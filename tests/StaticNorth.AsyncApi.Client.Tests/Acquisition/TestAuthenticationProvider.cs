using StaticNorth.AsyncApi.Client.Acquisition;

namespace StaticNorth.AsyncApi.Client.Tests.Acquisition;

internal sealed class TestAuthenticationProvider(Action<HttpRequestMessage> authenticate) : ISpecificationAuthenticationProvider
{
    public ValueTask AuthenticateAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        authenticate(request);
        return ValueTask.CompletedTask;
    }
}
