namespace StaticNorth.AsyncApi.Client.Acquisition;

/// <summary>Supplies authentication headers for an AsyncAPI document request.</summary>
public interface ISpecificationAuthenticationProvider
{
    /// <summary>Adds authentication headers to the outgoing request.</summary>
    /// <param name="request">The request to authenticate. Only request headers other than Host may be changed.</param>
    /// <param name="cancellationToken">A token that requests cancellation of authentication.</param>
    /// <returns>A task that completes when the request is ready to send.</returns>
    ValueTask AuthenticateAsync(HttpRequestMessage request, CancellationToken cancellationToken = default);
}
