using StaticNorth.AsyncApi.Generator;

[AsyncApiDefinition]
internal sealed class PublishOrderCreated : IAsyncApiOperationDefinition
{
    public void Configure(IOperationBuilder operation)
    {
        operation
            .Name("publishOrderCreated")
            .Channel("orders")
            .Send()
            .Message("order.created")
            .Description("Publishes the new order identifier.");
    }
}
