using System.Runtime.ExceptionServices;
using StaticNorth.AsyncApi.Core.Transport;

namespace StaticNorth.AsyncApi.Client.Transport;

public sealed class InMemoryAsyncApiTransport : IAsyncApiTransport
{
    private readonly Lock _gate = new();
    private readonly List<InMemorySubscription> _subscriptions = [];

    public ValueTask<IAsyncDisposable> SubscribeAsync(
        AsyncApiMessageRoute route,
        Func<AsyncApiEnvelope, CancellationToken, ValueTask> handler,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(route);
        ArgumentNullException.ThrowIfNull(handler);
        var subscription = new InMemorySubscription(route, handler, Remove);

        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _subscriptions.Add(subscription);
        }

        return ValueTask.FromResult<IAsyncDisposable>(subscription);
    }

    public async ValueTask PublishAsync(AsyncApiEnvelope message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        cancellationToken.ThrowIfCancellationRequested();
        InMemorySubscription[] snapshot;

        lock (_gate)
        {
            snapshot = _subscriptions
                .Where(subscription => subscription.Route.ChannelAddress == message.Route.ChannelAddress &&
                    subscription.Route.MessageKey == message.Route.MessageKey)
                .ToArray();
        }

        List<Exception>? errors = null;

        foreach (var subscription in snapshot)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await subscription
                    .Handler(message, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                (errors ??= []).Add(exception);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (errors is [var error])
        {
            ExceptionDispatchInfo.Throw(error);
        }

        if (errors is not null)
        {
            throw new AggregateException(errors);
        }
    }

    private void Remove(InMemorySubscription subscription)
    {
        lock (_gate)
        {
            _subscriptions.Remove(subscription);
        }
    }
}
