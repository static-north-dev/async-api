using StaticNorth.AsyncApi.Generator;

[AsyncApiDefinition]
internal sealed class OrdersChannel : IAsyncApiChannelDefinition
{
    public void Configure(IChannelBuilder channel)
    {
        channel
            .Name("orders")
            .Address("orders.created")
            .Message("order.created");
    }
}
