using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Runtime.ExceptionServices;
using StaticNorth.AsyncApi.Core.Transport;

namespace StaticNorth.AsyncApi.Client.Transport;

public sealed class InMemoryAsyncApiTransport : IAsyncApiTransport
{
    private readonly Lock _gate = new();
    private readonly ConcurrentDictionary<(string Address, string MessageKey), ImmutableArray<InMemorySubscription>> _subscriptions = new();

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
            var key = (route.ChannelAddress, route.MessageKey);
            _subscriptions[key] = _subscriptions.TryGetValue(key, out var snapshot)
                ? snapshot.Add(subscription)
                : [subscription];
        }

        return ValueTask.FromResult<IAsyncDisposable>(subscription);
    }

    public async ValueTask PublishAsync(AsyncApiEnvelope message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        cancellationToken.ThrowIfCancellationRequested();
        var key = (message.Route.ChannelAddress, message.Route.MessageKey);

        if (!_subscriptions.TryGetValue(key, out var snapshot))
        {
            return;
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
            var route = subscription.Route;
            var key = (route.ChannelAddress, route.MessageKey);

            if (!_subscriptions.TryGetValue(key, out var snapshot))
            {
                return;
            }

            var remaining = snapshot.Remove(subscription);

            if (remaining.IsEmpty)
            {
                _subscriptions.TryRemove(key, out _);
            }
            else if (remaining != snapshot)
            {
                _subscriptions[key] = remaining;
            }
        }
    }
}
