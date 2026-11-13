using System.Text.Json.Nodes;
using StaticNorth.AsyncApi.Generator;

namespace Producer;

[AsyncApiDefinition]
internal sealed class OrderCreatedDefinition : IAsyncApiMessageDefinition<OrderCreated>
{
    public void Configure(IMessageBuilder<OrderCreated> message)
    {
        var payload = OrderCreated.Example();

        message
            .Id("OrderCreated")
            .Name("order.created")
            .Description("Emitted when an order is created")
            .Header<string>("X-Correlation-Id", required: true)
            .CorrelationId("$message.header#/X-Correlation-Id")
            .Example(payload, new JsonObject { ["X-Correlation-Id"] = "example-1" });
    }
}
