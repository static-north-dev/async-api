using StaticNorth.AsyncApi.Generator;

[AsyncApiDefinition]
internal sealed class OrderMessage : IAsyncApiMessageDefinition<string>
{
    public void Configure(IMessageBuilder<string> message)
    {
        message
            .Name("order.created")
            .Example("order-123");
    }
}
