using System.Text.Json.Nodes;

namespace StaticNorth.AsyncApi.Generator;

public interface IOperationBuilder
{
    IOperationBuilder Name(string id);
    IOperationBuilder Channel(string id);
    IOperationBuilder Send();
    IOperationBuilder Receive();
    IOperationBuilder Description(string description);
    IOperationBuilder Message(string channelMessageKey);
    IOperationBuilder Trait(string id);
    IOperationBuilder Bindings(JsonObject bindings);
    IOperationBuilder WithExtension(string name, JsonNode? value);
}
