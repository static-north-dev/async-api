namespace StaticNorth.AsyncApi.Core.Transport;

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

    public string ChannelAddress { get; } = RequireValue(channelAddress, nameof(channelAddress));
    public string MessageKey { get; } = RequireValue(messageKey, nameof(messageKey));
    public string OperationId { get; } = RequireValue(operationId, nameof(operationId));
    public string ContentType { get; } = RequireValue(contentType, nameof(contentType));
    public string? ChannelBindingsJson { get; } = channelBindingsJson;
    public string? OperationBindingsJson { get; } = operationBindingsJson;
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
