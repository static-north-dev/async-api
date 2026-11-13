using System.Net.Http.Headers;
using StaticNorth.AsyncApi.Client.Acquisition;

internal sealed class EnvironmentTokenProvider(string token) : ISpecificationAuthenticationProvider
{
    public ValueTask AuthenticateAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return ValueTask.CompletedTask;
    }
}
