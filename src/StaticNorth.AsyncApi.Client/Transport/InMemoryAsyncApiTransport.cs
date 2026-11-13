using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Runtime.ExceptionServices;
using StaticNorth.AsyncApi.Core.Transport;

namespace StaticNorth.AsyncApi.Client.Transport;

/// <summary>Delivers messages to subscribers in the same process by channel address and message key.</summary>
/// <remarks>Subscriptions belong to this instance. The transport provides no persistence, retries, or acknowledgements.</remarks>
public sealed class InMemoryAsyncApiTransport : IAsyncApiTransport
{
    private readonly Lock _gate = new();
    private readonly ConcurrentDictionary<(string Address, string MessageKey), ImmutableArray<InMemorySubscription>> _subscriptions = new();

    /// <inheritdoc />
    /// <remarks>Disposal removes the handler from future publishes. A publish that already captured the subscription can still invoke it.</remarks>
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

    /// <inheritdoc />
    /// <remarks>Each publish invokes matching handlers in registration order. Concurrent publishes can invoke the same handler concurrently. A handler failure is reported after the remaining handlers run unless delivery is canceled.</remarks>
    /// <exception cref="AggregateException">More than one handler fails during the publish.</exception>
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
