using System.Collections.Frozen;

namespace StaticNorth.AsyncApi.Core.Transport;

public sealed class AsyncApiEnvelope
{
    public AsyncApiEnvelope(
        AsyncApiMessageRoute route,
        ReadOnlyMemory<byte> payload,
        IReadOnlyDictionary<string, string>? headers = null,
        string? correlationId = null)
    {
        ArgumentNullException.ThrowIfNull(route);
        Route = route;
        Payload = payload.ToArray();
        Headers = headers?.ToFrozenDictionary(StringComparer.Ordinal) ?? FrozenDictionary<string, string>.Empty;
        CorrelationId = correlationId;
    }

    public AsyncApiMessageRoute Route { get; }
    public ReadOnlyMemory<byte> Payload { get; }
    public IReadOnlyDictionary<string, string> Headers { get; }
    public string? CorrelationId { get; }
}
