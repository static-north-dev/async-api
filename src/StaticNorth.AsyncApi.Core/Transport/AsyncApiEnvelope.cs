using System.Collections.Frozen;

namespace StaticNorth.AsyncApi.Core.Transport;

/// <summary>Carries a message payload and metadata between a client and a transport.</summary>
public sealed class AsyncApiEnvelope
{
    /// <summary>Creates an envelope by copying the supplied payload and headers.</summary>
    /// <param name="route">The route and content metadata for the message.</param>
    /// <param name="payload">The message bytes. Use empty memory for a message without a payload.</param>
    /// <param name="headers">Optional message headers. Header names are compared using ordinal rules.</param>
    /// <param name="correlationId">An optional identifier for correlating related messages.</param>
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

    /// <summary>Gets the route and content metadata for the message.</summary>
    public AsyncApiMessageRoute Route { get; }
    /// <summary>Gets the message bytes. Treat the underlying buffer as read-only.</summary>
    public ReadOnlyMemory<byte> Payload { get; }
    /// <summary>Gets the message headers, or an empty dictionary if none were supplied.</summary>
    public IReadOnlyDictionary<string, string> Headers { get; }
    /// <summary>Gets the correlation identifier, if present.</summary>
    public string? CorrelationId { get; }

    // The caller transfers exclusive ownership of the array for the lifetime of the envelope.
    internal static AsyncApiEnvelope FromOwnedPayload(AsyncApiMessageRoute route, byte[] payload)
    {
        return new AsyncApiEnvelope(route, payload, null, null, takeOwnership: true);
    }
}
