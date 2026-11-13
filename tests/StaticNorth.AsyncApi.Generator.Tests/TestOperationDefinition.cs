namespace StaticNorth.AsyncApi.Generator.Tests;

internal sealed class TestOperationDefinition(string channel = "test", string key = "missing") : IAsyncApiOperationDefinition
{
    public void Configure(IOperationBuilder operation)
    {
        operation
            .Name("sendText")
            .Channel(channel)
            .Send()
            .Message(key);
    }
}
