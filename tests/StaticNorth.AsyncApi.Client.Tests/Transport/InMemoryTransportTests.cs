using System.Text.Json;
using StaticNorth.AsyncApi.Client.Transport;
using StaticNorth.AsyncApi.Core.Transport;

namespace StaticNorth.AsyncApi.Client.Tests.Transport;

public sealed class InMemoryTransportTests
{
    private static readonly AsyncApiMessageRoute Route = new("orders.eu", "created", "subscribe", "application/json");

    [Test]
    public async Task RoutesToMatchingSubscriptionsAndPreservesEnvelopeData()
    {
        var transport = new InMemoryAsyncApiTransport();
        var payload = new byte[] { 42 };
        var headers = new Dictionary<string, string> { ["X-Trace"] = "original" };
        var route = new AsyncApiMessageRoute("orders.eu", "created", "publish", "application/json", messageBindingsJson: "{}");
        var envelope = new AsyncApiEnvelope(route, payload, headers, "correlation");
        payload[0] = 0;
        headers["X-Trace"] = "mutated";
        AsyncApiEnvelope? received = null;
        using var cancellation = new CancellationTokenSource();
        var receivedToken = CancellationToken.None;
        var subscription = await transport.SubscribeAsync(Route, (message, token) =>
        {
            received = message;
            receivedToken = token;
            return ValueTask.CompletedTask;
        });

        var otherRoute = new AsyncApiMessageRoute("orders.eu", "updated", "subscribe", "application/json");
        await using var other = await transport.SubscribeAsync(otherRoute, (_, _) => throw new InvalidOperationException());

        try
        {
            await transport.PublishAsync(envelope, cancellation.Token);
            var delivered = received ?? throw new InvalidOperationException("The envelope was not delivered.");

            await Assert
                .That(delivered.Payload.Span[0])
                .IsEqualTo((byte)42);
            await Assert
                .That(delivered.Headers["X-Trace"])
                .IsEqualTo("original");
            await Assert
                .That(delivered.CorrelationId)
                .IsEqualTo("correlation");
            await Assert
                .That(delivered.Route.MessageBindingsJson)
                .IsEqualTo("{}");
            await Assert
                .That(receivedToken)
                .IsEqualTo(cancellation.Token);
        }
        finally
        {
            await subscription.DisposeAsync();
        }

        received = null;
        await transport.PublishAsync(envelope);
        await Assert
            .That(received)
            .IsNull();
    }

    [Test]
    [Arguments("invalid", "application/json")]
    [Arguments("42", "text/plain")]
    public async Task ReportsInvalidDeliveryAfterDeliveringToOtherSubscribers(string payload, string contentType)
    {
        var transport = new InMemoryAsyncApiTransport();
        var client = new AsyncApiJsonClient(transport);
        await using var typed = await client.SubscribeAsync<int>(Route, (_, _) => throw new InvalidOperationException(), false, default);
        var delivered = false;
        await using var other = await transport.SubscribeAsync(Route, (_, _) =>
        {
            delivered = true;
            return ValueTask.CompletedTask;
        });
        var route = new AsyncApiMessageRoute("orders.eu", "created", "publish", contentType);
        var envelope = new AsyncApiEnvelope(route, System.Text.Encoding.UTF8.GetBytes(payload));

        await Assert
            .That(async () => await transport.PublishAsync(envelope))
            .Throws<JsonException>();
        await Assert
            .That(delivered)
            .IsTrue();
    }

    [Test]
    public async Task CancellationPreventsRegistrationAndPublish()
    {
        var transport = new InMemoryAsyncApiTransport();
        var canceled = new CancellationToken(canceled: true);
        var envelope = new AsyncApiEnvelope(Route, ReadOnlyMemory<byte>.Empty);

        await Assert
            .That(async () => await transport.SubscribeAsync(Route, (_, _) => throw new InvalidOperationException(), canceled))
            .Throws<OperationCanceledException>();
        await Assert
            .That(async () => await transport.PublishAsync(envelope, canceled))
            .Throws<OperationCanceledException>();
        await transport.PublishAsync(envelope);
    }

    [Test]
    public async Task SubscriptionChangesAffectOnlyFuturePublishes()
    {
        var transport = new InMemoryAsyncApiTransport();
        var envelope = new AsyncApiEnvelope(Route, ReadOnlyMemory<byte>.Empty);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var deliveries = new List<int>();
        await using var first = await transport.SubscribeAsync(Route, async (_, token) =>
        {
            deliveries.Add(1);
            started.TrySetResult();
            await resume.Task.WaitAsync(token);
        });
        await using var second = await transport.SubscribeAsync(Route, (_, _) =>
        {
            deliveries.Add(2);
            return ValueTask.CompletedTask;
        });

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var pending = transport.PublishAsync(envelope, timeout.Token);
        await started.Task.WaitAsync(timeout.Token);
        await second.DisposeAsync();
        await using var third = await transport.SubscribeAsync(Route, (_, _) =>
        {
            deliveries.Add(3);
            return ValueTask.CompletedTask;
        });
        resume.SetResult();
        await pending;
        await transport.PublishAsync(envelope, timeout.Token);

        await Assert
            .That(deliveries.SequenceEqual([1, 2, 1, 3]))
            .IsTrue();
    }

    [Test]
    public async Task PayloadlessMessagesDeliverEmptyBytes()
    {
        var transport = new InMemoryAsyncApiTransport();
        var client = new AsyncApiJsonClient(transport);
        var deliveries = 0;
        await using var subscription = await client.SubscribeAsync(Route, _ =>
        {
            deliveries++;
            return ValueTask.CompletedTask;
        }, default);

        await client.PublishAsync(Route, default);
        await Assert
            .That(deliveries)
            .IsEqualTo(1);

        var unexpectedPayload = new AsyncApiEnvelope(Route, "null"u8.ToArray());
        await Assert
            .That(async () => await transport.PublishAsync(unexpectedPayload))
            .Throws<JsonException>();
        await Assert
            .That(deliveries)
            .IsEqualTo(1);
    }

    [Test]
    public async Task PayloadlessPublishesReuseTheEnvelopeForTheSameRoute()
    {
        var transport = new InMemoryAsyncApiTransport();
        var client = new AsyncApiJsonClient(transport);
        var envelopes = new List<AsyncApiEnvelope>();
        await using var subscription = await transport.SubscribeAsync(Route, (envelope, _) =>
        {
            envelopes.Add(envelope);
            return ValueTask.CompletedTask;
        });

        await client.PublishAsync(Route, default);
        await client.PublishAsync(Route, default);
        var otherRoute = new AsyncApiMessageRoute(Route.ChannelAddress, Route.MessageKey, "other", "application/json");
        await client.PublishAsync(otherRoute, default);

        await Assert
            .That(envelopes.Count == 3 &&
                ReferenceEquals(envelopes[0], envelopes[1]) &&
                ReferenceEquals(envelopes[0].Route, Route) &&
                ReferenceEquals(envelopes[2].Route, otherRoute) &&
                envelopes.TrueForAll(static envelope => envelope.Payload.IsEmpty))
            .IsTrue();
    }
}
