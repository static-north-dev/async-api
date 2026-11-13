using System.Text.Json.Nodes;
using StaticNorth.AsyncApi.Generator;

namespace Producer;

[AsyncApiDefinition]
internal sealed class OrderCreatedDefinition : IAsyncApiMessageDefinition<OrderCreated>
{
    public void Configure(IMessageBuilder<OrderCreated> message)
    {
        var payload = new OrderCreated(
            Guid.Empty,
            DateTimeOffset.UnixEpoch,
            OrderStatus.Created,
            [new OrderItem("ABC-123", 12.50m)])
        {
            Source = "checkout"
        };

        message
            .Id("OrderCreated")
            .Name("order.created")
            .Description("Emitted when an order is created")
            .Header<string>("X-Correlation-Id", required: true)
            .CorrelationId("$message.header#/X-Correlation-Id")
            .Example(payload, new JsonObject { ["X-Correlation-Id"] = "example-1" });
    }
}
