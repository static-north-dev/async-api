using StaticNorth.AsyncApi.Generator;

namespace Producer;

[AsyncApiDefinition]
internal sealed class PublishOrderCreatedDefinition : IAsyncApiOperationDefinition
{
    public void Configure(IOperationBuilder operation)
    {
        operation
            .Name("publishOrderCreated")
            .Channel("orders")
            .Send()
            .Message("orderCreated")
            .Trait("documented");
    }
}
