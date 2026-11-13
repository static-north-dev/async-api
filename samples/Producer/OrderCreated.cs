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

    internal static OrderCreated Example()
    {
        return new OrderCreated(
            Guid.Parse("dc572c8c-131a-40d5-8d37-091b02971883"),
            DateTimeOffset.UnixEpoch,
            OrderStatus.Created,
            [new OrderItem("ABC-123", 12.50m)])
        {
            Source = "checkout"
        };
    }
}
