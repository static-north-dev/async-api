using StaticNorth.AsyncApi.Core.Transport;

namespace StaticNorth.AsyncApi.Client.Transport;

internal sealed class InMemorySubscription(
    AsyncApiMessageRoute route,
    Func<AsyncApiEnvelope, CancellationToken, ValueTask> handler,
    Action<InMemorySubscription> remove) : IAsyncDisposable
{
    internal AsyncApiMessageRoute Route { get; } = route;
    internal Func<AsyncApiEnvelope, CancellationToken, ValueTask> Handler { get; } = handler;

    public ValueTask DisposeAsync()
    {
        remove(this);
        return ValueTask.CompletedTask;
    }
}
