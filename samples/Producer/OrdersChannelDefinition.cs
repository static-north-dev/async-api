using System.Text.Json.Nodes;
using StaticNorth.AsyncApi.Generator;

namespace Producer;

[AsyncApiDefinition]
internal sealed class OrdersChannelDefinition : IAsyncApiChannelDefinition
{
    public void Configure(IChannelBuilder channel)
    {
        channel
            .Name("orders")
            .Address("orders.{region}")
            .Parameter("region", new JsonObject { ["description"] = "Deployment region" })
            .Server("broker")
            .Message("OrderCreated", "orderCreated");
    }
}
