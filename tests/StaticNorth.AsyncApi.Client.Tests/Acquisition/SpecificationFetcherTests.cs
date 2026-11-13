using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using StaticNorth.AsyncApi.Client.Acquisition;

namespace StaticNorth.AsyncApi.Client.Tests.Acquisition;

public sealed class SpecificationFetcherTests
{
    private const string ValidDocument = """{"asyncapi":"3.1.0","info":{"title":"Orders","version":"1"}}""";
    private static readonly Uri Source = new("https://contracts.example.test/orders?token=secret");

    [Test]
    public async Task RefreshesWithAnAuthenticationProviderAndVerifiesTheOfflineChecksum()
    {
        var directory = Directory.CreateTempSubdirectory();

        try
        {
            var path = Path.Combine(directory.FullName, "orders.asyncapi.json");
            var checksum = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ValidDocument)));
            string? authorization = null;
            using var fetcher = new SpecificationFetcher(new FetchHandler(request =>
            {
                authorization = request.Headers.Authorization?.Parameter;
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(ValidDocument) };
            }));
            var authentication = new TestAuthenticationProvider(request =>
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "secret");
            });
            await fetcher.RefreshAsync(Source, path, authentication, checksum);
            var document = await SpecificationFetcher.ReadOfflineAsync(path, checksum);

            await Assert
                .That(authorization)
                .IsEqualTo("secret");
            await Assert
                .That(document.Info.Title)
                .IsEqualTo("Orders");
            await Assert
                .That(async () =>
                {
                    await SpecificationFetcher.ReadOfflineAsync(path, new string('0', 64));
                })
                .Throws<InvalidOperationException>();
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Test]
    [Arguments(401, "secret")]
    [Arguments(302, "secret")]
    [Arguments(200, "{secret")]
    [Arguments(200, """{"asyncapi":"3.1.0","info":{"title":"Orders","version":"1"},"operations":{"bad":{"action":"secret"}}}""")]
    public async Task FailedRefreshPreservesThePreviousFileAndRedactsSensitiveDetails(int status, string response)
    {
        var directory = Directory.CreateTempSubdirectory();

        try
        {
            var path = Path.Combine(directory.FullName, "orders.asyncapi.json");
            await File.WriteAllTextAsync(path, ValidDocument);
            using var fetcher = new SpecificationFetcher(new FetchHandler(_ =>
                new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent(response) }));
            var error = await Assert
                .That(() => fetcher.RefreshAsync(Source, path))
                .Throws<InvalidOperationException>();
            var stored = await File.ReadAllTextAsync(path);

            await Assert
                .That(error!.ToString())
                .DoesNotContain("secret");
            await Assert
                .That(stored)
                .IsEqualTo(ValidDocument);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Test]
    public async Task RejectsChangingTheAuthenticationOriginBeforeSending()
    {
        using var fetcher = new SpecificationFetcher(new FetchHandler(_ =>
            throw new NotSupportedException("The HTTP handler should not run.")));
        var authentication = new TestAuthenticationProvider(request =>
        {
            request.RequestUri = new Uri("https://different.example.test/orders");
        });

        await Assert
            .That(() => fetcher.RefreshAsync(Source, "unused.json", authentication))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task RedactsAuthenticationProviderFailures()
    {
        using var fetcher = new SpecificationFetcher(new FetchHandler(_ =>
            throw new NotSupportedException("The HTTP handler should not run.")));
        var authentication = new TestAuthenticationProvider(_ =>
            throw new InvalidOperationException("provider-secret"));
        var error = await Assert
            .That(() => fetcher.RefreshAsync(Source, "unused.json", authentication))
            .Throws<InvalidOperationException>();

        await Assert
            .That(error!.ToString())
            .DoesNotContain("secret");
    }
}
