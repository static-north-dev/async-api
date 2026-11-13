using System.Text.Json.Nodes;

namespace StaticNorth.AsyncApi.Generator.Tests;

internal sealed class TextMessageDefinition(JsonObject schema, string id) : IAsyncApiMessageDefinition<string>
{
    public void Configure(IMessageBuilder<string> message)
    {
        message
            .Id(id)
            .Name("text")
            .PayloadSchema(schema);
    }
}
