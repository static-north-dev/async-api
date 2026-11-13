using System.Collections.Frozen;

namespace StaticNorth.AsyncApi.Core.Transport;

public sealed class AsyncApiEnvelope
{
    public AsyncApiEnvelope(
        AsyncApiMessageRoute route,
        ReadOnlyMemory<byte> payload,
        IReadOnlyDictionary<string, string>? headers = null,
        string? correlationId = null)
        : this(route, payload, headers, correlationId, takeOwnership: false)
    {
    }

    private AsyncApiEnvelope(
        AsyncApiMessageRoute route,
        ReadOnlyMemory<byte> payload,
        IReadOnlyDictionary<string, string>? headers,
        string? correlationId,
        bool takeOwnership)
    {
        ArgumentNullException.ThrowIfNull(route);
        Route = route;
        Payload = takeOwnership ? payload : payload.ToArray();
        Headers = headers?.ToFrozenDictionary(StringComparer.Ordinal) ?? FrozenDictionary<string, string>.Empty;
        CorrelationId = correlationId;
    }

    public AsyncApiMessageRoute Route { get; }
    public ReadOnlyMemory<byte> Payload { get; }
    public IReadOnlyDictionary<string, string> Headers { get; }
    public string? CorrelationId { get; }

    // The caller transfers exclusive ownership of the array for the lifetime of the envelope.
    internal static AsyncApiEnvelope FromOwnedPayload(AsyncApiMessageRoute route, byte[] payload)
    {
        return new AsyncApiEnvelope(route, payload, null, null, takeOwnership: true);
    }
}
