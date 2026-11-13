namespace StaticNorth.AsyncApi.Client.Acquisition;

public interface ISpecificationAuthenticationProvider
{
    ValueTask AuthenticateAsync(HttpRequestMessage request, CancellationToken cancellationToken = default);
}
