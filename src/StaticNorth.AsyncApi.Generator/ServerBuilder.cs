using System.Text.Json.Nodes;

namespace StaticNorth.AsyncApi.Generator;

internal sealed class ServerBuilder : IServerBuilder
{
    internal JsonObject Value { get; } = new();

    public IServerBuilder Host(string host)
    {
        Value["host"] = DocumentJson.RequireName(host, "server host");
        return this;
    }

    public IServerBuilder Protocol(string protocol, string? version = null)
    {
        Value["protocol"] = DocumentJson.RequireName(protocol, "server protocol");

        if (version is not null)
        {
            Value["protocolVersion"] = version;
        }
        else
        {
            Value.Remove("protocolVersion");
        }

        return this;
    }

    public IServerBuilder Description(string description)
    {
        Value["description"] = description;
        return this;
    }

    public IServerBuilder Bindings(JsonObject bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        Value["bindings"] = bindings.DeepClone();
        return this;
    }

    public IServerBuilder WithExtension(string name, JsonNode? value)
    {
        DocumentJson.SetExtension(Value, name, value);
        return this;
    }
}
