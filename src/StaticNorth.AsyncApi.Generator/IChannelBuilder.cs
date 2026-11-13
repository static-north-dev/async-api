using System.Text.Json.Nodes;

namespace StaticNorth.AsyncApi.Generator;

public interface IChannelBuilder
{
    IChannelBuilder Name(string id);
    IChannelBuilder Address(string? address);
    IChannelBuilder Description(string description);
    IChannelBuilder Message(string messageId, string? key = null);
    IChannelBuilder Server(string id);
    IChannelBuilder Parameter(string name, JsonObject parameter);
    IChannelBuilder Bindings(JsonObject bindings);
    IChannelBuilder WithExtension(string name, JsonNode? value);
}
