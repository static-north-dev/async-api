using System.Text.Json;
using StaticNorth.AsyncApi.Core.Transport;

namespace StaticNorth.AsyncApi.Client.Transport;

public sealed class AsyncApiJsonClient
{
    private readonly IAsyncApiTransport _transport;
    private readonly JsonSerializerOptions _options;

    public AsyncApiJsonClient(IAsyncApiTransport transport, JsonSerializerOptions? serializerOptions = null)
    {
        ArgumentNullException.ThrowIfNull(transport);
        _transport = transport;
        _options = serializerOptions is null
            ? new JsonSerializerOptions { RespectNullableAnnotations = true }
            : new JsonSerializerOptions(serializerOptions);
    }

    public static string ResolveAddress(string? declaredAddress, string? channelAddress)
    {
        var address = channelAddress ?? declaredAddress;

        if (string.IsNullOrWhiteSpace(address) || address.Contains('{') || address.Contains('}'))
        {
            throw new ArgumentException("Supply a concrete channel address for an absent or templated address.", nameof(channelAddress));
        }

        return address;
    }

    public ValueTask PublishAsync<T>(AsyncApiMessageRoute route, T message, bool allowsNull, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(route);

        if (!allowsNull && message is null)
        {
            throw new ArgumentNullException(nameof(message));
        }

        var payload = JsonSerializer.SerializeToUtf8Bytes(message, _options);
        return _transport.PublishAsync(new AsyncApiEnvelope(route, payload), cancellationToken);
    }

    public ValueTask PublishAsync(AsyncApiMessageRoute route, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _transport.PublishAsync(new AsyncApiEnvelope(route, ReadOnlyMemory<byte>.Empty), cancellationToken);
    }

    public ValueTask<IAsyncDisposable> SubscribeAsync<T>(
        AsyncApiMessageRoute route,
        Func<T, CancellationToken, ValueTask> handler,
        bool allowsNull,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(route);
        ArgumentNullException.ThrowIfNull(handler);
        cancellationToken.ThrowIfCancellationRequested();

        return _transport.SubscribeAsync(route, (envelope, token) =>
        {
            token.ThrowIfCancellationRequested();

            var actual = MediaType(envelope.Route.ContentType);
            var expected = MediaType(route.ContentType);

            if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
            {
                throw new JsonException($"Expected content type '{route.ContentType}', received '{envelope.Route.ContentType}'.");
            }

            var value = JsonSerializer.Deserialize<T>(envelope.Payload.Span, _options);

            if (value is null && !allowsNull)
            {
                throw new JsonException($"Message '{route.MessageKey}' requires a non-null payload.");
            }

            return handler(value!, token);
        }, cancellationToken);
    }

    public ValueTask<IAsyncDisposable> SubscribeAsync(
        AsyncApiMessageRoute route,
        Func<CancellationToken, ValueTask> handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(route);
        ArgumentNullException.ThrowIfNull(handler);
        cancellationToken.ThrowIfCancellationRequested();

        return _transport.SubscribeAsync(route, (envelope, token) =>
        {
            token.ThrowIfCancellationRequested();

            if (!envelope.Payload.IsEmpty)
            {
                throw new JsonException($"Message '{route.MessageKey}' declares no payload.");
            }

            return handler(token);
        }, cancellationToken);
    }

    private static ReadOnlySpan<char> MediaType(string contentType)
    {
        var separator = contentType.IndexOf(';', StringComparison.Ordinal);
        var value = separator < 0 ? contentType.AsSpan() : contentType.AsSpan(0, separator);
        return value.Trim();
    }
}
