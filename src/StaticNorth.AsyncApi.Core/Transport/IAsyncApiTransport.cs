namespace StaticNorth.AsyncApi.Core.Transport;

public interface IAsyncApiTransport
{
    ValueTask PublishAsync(AsyncApiEnvelope message, CancellationToken cancellationToken = default);

    ValueTask<IAsyncDisposable> SubscribeAsync(
        AsyncApiMessageRoute route,
        Func<AsyncApiEnvelope, CancellationToken, ValueTask> handler,
        CancellationToken cancellationToken = default);
}
