namespace StaticNorth.AsyncApi.Core.Transport;

/// <summary>Defines the transport operations used by AsyncAPI clients.</summary>
public interface IAsyncApiTransport
{
    /// <summary>Publishes a message on the envelope's route.</summary>
    /// <param name="message">The envelope to publish. Treat its contents as read-only.</param>
    /// <param name="cancellationToken">A token that requests cancellation of the publish.</param>
    /// <returns>A task that completes according to the transport's publish semantics.</returns>
    ValueTask PublishAsync(AsyncApiEnvelope message, CancellationToken cancellationToken = default);

    /// <summary>Registers a handler for messages on a route.</summary>
    /// <param name="route">The route to subscribe to.</param>
    /// <param name="handler">The callback that receives each envelope and a delivery cancellation token.</param>
    /// <param name="cancellationToken">A token that requests cancellation of the subscription operation.</param>
    /// <returns>A handle to dispose when the subscription is no longer needed.</returns>
    ValueTask<IAsyncDisposable> SubscribeAsync(
        AsyncApiMessageRoute route,
        Func<AsyncApiEnvelope, CancellationToken, ValueTask> handler,
        CancellationToken cancellationToken = default);
}
