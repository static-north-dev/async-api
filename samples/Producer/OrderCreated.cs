using System.Text.Json.Serialization;

namespace Producer;

internal sealed record OrderCreated(
    [property: JsonPropertyName("order_id")] Guid OrderId,
    DateTimeOffset CreatedAt,
    OrderStatus Status,
    IReadOnlyList<OrderItem> Items,
    string? Note = null)
{
    [JsonIgnore]
    public string InternalNote { get; init; } = "";

    public required string Source { get; init; }
}
