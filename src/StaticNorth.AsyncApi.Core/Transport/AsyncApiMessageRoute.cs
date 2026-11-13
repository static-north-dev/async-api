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
    public string ChannelAddress { get; } = RequireValue(channelAddress, nameof(channelAddress));
    public string MessageKey { get; } = RequireValue(messageKey, nameof(messageKey));
    public string OperationId { get; } = RequireValue(operationId, nameof(operationId));
    public string ContentType { get; } = RequireValue(contentType, nameof(contentType));
    public string? ChannelBindingsJson { get; } = channelBindingsJson;
    public string? OperationBindingsJson { get; } = operationBindingsJson;
    public string? MessageBindingsJson { get; } = messageBindingsJson;

    private static string RequireValue(string value, string parameter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameter);
        return value;
    }
}
