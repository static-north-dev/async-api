using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using AsyncApi.Generated.Benchmark;
using StaticNorth.AsyncApi.Benchmarks;
using StaticNorth.AsyncApi.Client.Transport;
using StaticNorth.AsyncApi.Core.Transport;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
Console.WriteLine($"{RuntimeInformation.FrameworkDescription} | {RuntimeInformation.OSDescription} | {RuntimeInformation.ProcessArchitecture}");
Console.WriteLine("Scenario                                     ops/s        B/op     p95 (µs)");

var empty = new InMemoryAsyncApiTransport();
var publisher = new SendPublisher(empty);
await ReleaseProbe.RunAsync("Default JSON client construction", () =>
{
    GC.KeepAlive(new AsyncApiJsonClient(empty));
    return ValueTask.CompletedTask;
});
await ReleaseProbe.RunAsync("Generated empty publish", () => publisher.PublishHeartbeatAsync());

foreach (var size in new[] { 64, 1024 })
{
    var payload = new string('x', size);
    var bytes = JsonSerializer.SerializeToUtf8Bytes(payload);
    var route = new AsyncApiMessageRoute("benchmark.messages", "data", "send", "application/json");
    var envelope = new AsyncApiEnvelope(route, bytes);
    var receiving = new InMemoryAsyncApiTransport();
    var subscriber = new ReceiveSubscriber(receiving);
    await using var subscription = await subscriber.SubscribeDataAsync((message, _) =>
    {
        if (message.Length != size)
        {
            throw new InvalidOperationException("Unexpected payload size.");
        }

        return ValueTask.CompletedTask;
    });

    var asyncTransport = new InMemoryAsyncApiTransport();
    var asyncSubscriber = new ReceiveSubscriber(asyncTransport);
    await using var asyncSubscription = await asyncSubscriber.SubscribeDataAsync(async (message, token) =>
    {
        await Task.Yield();
        token.ThrowIfCancellationRequested();

        if (message.Length != size)
        {
            throw new InvalidOperationException("Unexpected payload size.");
        }
    });

    var concurrent = new SendPublisher(asyncTransport);
    await ReleaseProbe.RunAsync($"Typed publish only ({bytes.Length} B)", () => publisher.PublishDataAsync(payload));
    await ReleaseProbe.RunAsync($"Typed receive, sync handler ({bytes.Length} B)", () => receiving.PublishAsync(envelope));
    await ReleaseProbe.RunAsync($"Typed receive, async handler ({bytes.Length} B)", () => asyncTransport.PublishAsync(envelope));
    await ReleaseProbe.RunAsync($"4 concurrent publishers ({bytes.Length} B)", () => concurrent.PublishDataAsync(payload), workers: 4);
}
