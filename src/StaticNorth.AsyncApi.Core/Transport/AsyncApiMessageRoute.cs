namespace StaticNorth.AsyncApi.Core.Transport;

/// <summary>Identifies a message on a concrete channel and carries operation and protocol metadata.</summary>
/// <param name="channelAddress">The concrete address used for delivery.</param>
/// <param name="messageKey">The message key within the channel.</param>
/// <param name="operationId">The ID of the AsyncAPI operation.</param>
/// <param name="contentType">The payload media type.</param>
/// <param name="channelBindingsJson">The channel bindings as JSON, if present.</param>
/// <param name="operationBindingsJson">The operation bindings as JSON, if present.</param>
/// <param name="messageBindingsJson">The message bindings as JSON, if present.</param>
public sealed class AsyncApiMessageRoute(
    string channelAddress,
    string messageKey,
    string operationId,
    string contentType,
    string? channelBindingsJson = null,
    string? operationBindingsJson = null,
    string? messageBindingsJson = null)
{
    private AsyncApiEnvelope? _emptyEnvelope;

    /// <summary>Gets the concrete address used for delivery.</summary>
    public string ChannelAddress { get; } = RequireValue(channelAddress, nameof(channelAddress));
    /// <summary>Gets the message key within the channel.</summary>
    public string MessageKey { get; } = RequireValue(messageKey, nameof(messageKey));
    /// <summary>Gets the ID of the AsyncAPI operation.</summary>
    public string OperationId { get; } = RequireValue(operationId, nameof(operationId));
    /// <summary>Gets the payload media type.</summary>
    public string ContentType { get; } = RequireValue(contentType, nameof(contentType));
    /// <summary>Gets the channel bindings as JSON, if present.</summary>
    public string? ChannelBindingsJson { get; } = channelBindingsJson;
    /// <summary>Gets the operation bindings as JSON, if present.</summary>
    public string? OperationBindingsJson { get; } = operationBindingsJson;
    /// <summary>Gets the message bindings as JSON, if present.</summary>
    public string? MessageBindingsJson { get; } = messageBindingsJson;

    internal AsyncApiEnvelope GetEmptyEnvelope()
    {
        var envelope = Volatile.Read(ref _emptyEnvelope);

        if (envelope is not null)
        {
            return envelope;
        }

        envelope = new AsyncApiEnvelope(this, ReadOnlyMemory<byte>.Empty);
        return Interlocked.CompareExchange(ref _emptyEnvelope, envelope, null) ?? envelope;
    }

    private static string RequireValue(string value, string parameter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameter);
        return value;
    }
}
