using System.Buffers;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using StaticNorth.AsyncApi.Core;

namespace StaticNorth.AsyncApi.Client.Acquisition;

public sealed class SpecificationFetcher : IDisposable
{
    private const int MaximumBytes = 4 * 1024 * 1024;
    private readonly HttpClient _client;

    public SpecificationFetcher()
        : this(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false })
    {
    }

    internal SpecificationFetcher(HttpMessageHandler handler)
    {
        _client = new HttpClient(handler);
    }

    public async Task RefreshAsync(
        Uri source,
        string destination,
        ISpecificationAuthenticationProvider? authentication = null,
        string? sha256 = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);

        if (!source.IsAbsoluteUri || source.Scheme is not ("http" or "https") ||
            source.UserInfo.Length != 0 || source.Fragment.Length != 0 ||
            (authentication is not null && source.Scheme != "https"))
        {
            throw new ArgumentException("Use an HTTP(S) URL without embedded credentials or fragments. Authentication requires HTTPS.", nameof(source));
        }

        var expected = ReadChecksum(sha256);
        cancellationToken.ThrowIfCancellationRequested();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_client.Timeout);
        var token = timeout.Token;
        using var request = new HttpRequestMessage(HttpMethod.Get, source);

        if (authentication is not null)
        {
            try
            {
                await authentication
                    .AuthenticateAsync(request, token)
                    .ConfigureAwait(false);
            }
            catch (Exception)
            {
                cancellationToken.ThrowIfCancellationRequested();
                throw new InvalidOperationException("The specification authentication provider failed.");
            }

            if (!string.Equals(source.OriginalString, request.RequestUri?.OriginalString, StringComparison.Ordinal) ||
                request.Method != HttpMethod.Get || request.Headers.Host is not null ||
                request.Content is not null)
            {
                throw new InvalidOperationException("The authentication provider may change request headers only, excluding Host.");
            }
        }

        ReadOnlyMemory<byte> data;

        try
        {
            using var response = await _client
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token)
                .ConfigureAwait(false);

            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw new InvalidOperationException($"Specification download failed with HTTP {(int)response.StatusCode}.");
            }

            if (response.Content.Headers.ContentLength > MaximumBytes)
            {
                throw new InvalidOperationException("The specification exceeds the 4 MiB limit.");
            }

            await using var stream = await response.Content
                .ReadAsStreamAsync(token)
                .ConfigureAwait(false);
            data = await ReadBoundedAsync(stream, token).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException ||
            (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            throw new InvalidOperationException("The specification download failed or timed out.");
        }

        Validate(data, expected);
        try
        {
            await ReplaceAsync(destination, data, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new InvalidOperationException("The specification refresh timed out.");
        }
    }

    public static async Task<AsyncApiDocument> ReadOfflineAsync(
        string path, string sha256, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(sha256);
        var expected = ReadChecksum(sha256);
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 8192,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var data = await ReadBoundedAsync(stream, cancellationToken).ConfigureAwait(false);
        return Validate(data, expected);
    }

    public void Dispose()
    {
        _client.Dispose();
    }

    private static async Task<ReadOnlyMemory<byte>> ReadBoundedAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = ArrayPool<byte>.Shared.Rent(8192);

        try
        {
            while (true)
            {
                var count = await stream
                    .ReadAsync(chunk.AsMemory(), cancellationToken)
                    .ConfigureAwait(false);

                if (count == 0)
                {
                    return buffer
                        .GetBuffer()
                        .AsMemory(0, (int)buffer.Length);
                }

                if (buffer.Length + count > MaximumBytes)
                {
                    throw new InvalidOperationException("The specification exceeds the 4 MiB limit.");
                }

                buffer.Write(chunk, 0, count);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(chunk, clearArray: true);
        }
    }

    private static AsyncApiDocument Validate(ReadOnlyMemory<byte> data, byte[]? expected)
    {
        if (expected is not null)
        {
            Span<byte> actual = stackalloc byte[SHA256.HashSizeInBytes];
            SHA256.HashData(data.Span, actual);

            if (!CryptographicOperations.FixedTimeEquals(actual, expected))
            {
                throw new InvalidOperationException("The specification SHA-256 checksum does not match.");
            }
        }

        try
        {
            var document = AsyncApiJson.Read(data);

            if (document.Operations is { } operations)
            {
                foreach (var operation in operations.EnumerateObject())
                {
                    var value = operation.Value;

                    if (value.ValueKind != JsonValueKind.Object ||
                        (!value.TryGetProperty("$ref", out _) &&
                         (!value.TryGetProperty("action", out var action) || action.ValueKind != JsonValueKind.String ||
                          action.GetString() is not ("send" or "receive"))))
                    {
                        throw new JsonException();
                    }
                }
            }

            return document;
        }
        catch (JsonException)
        {
            throw new InvalidOperationException("The response is not a supported AsyncAPI 3.1.0 JSON document.");
        }
    }

    private static byte[]? ReadChecksum(string? sha256)
    {
        if (sha256 is null)
        {
            return null;
        }

        if (sha256.Length != 64 || !sha256.All(Uri.IsHexDigit))
        {
            throw new ArgumentException("Use a 64-digit hexadecimal SHA-256 checksum.", nameof(sha256));
        }

        return Convert.FromHexString(sha256);
    }

    private static async Task ReplaceAsync(string destination, ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
    {
        var path = Path.GetFullPath(destination);
        var temporary = Path.Combine(Path.GetDirectoryName(path)!, $".asyncapi-{Guid.NewGuid():N}.tmp");
        var created = false;

        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 8192, useAsync: true))
            {
                created = true;
                await stream
                    .WriteAsync(data, cancellationToken)
                    .ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, path, overwrite: true);
            created = false;
        }
        finally
        {
            if (created)
            {
                File.Delete(temporary);
            }
        }
    }
}
