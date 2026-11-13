using System.Text.Json.Nodes;

namespace StaticNorth.AsyncApi.Generator;

public interface IServerBuilder
{
    IServerBuilder Host(string host);
    IServerBuilder Protocol(string protocol, string? version = null);
    IServerBuilder Description(string description);
    IServerBuilder Bindings(JsonObject bindings);
    IServerBuilder WithExtension(string name, JsonNode? value);
}
