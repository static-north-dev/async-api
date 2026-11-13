using System.Text.Json.Nodes;

namespace StaticNorth.AsyncApi.Generator;

internal sealed class ChannelBuilder : IChannelBuilder
{
    internal string? Id { get; private set; }
    internal JsonObject Value { get; } = new();
    internal Dictionary<string, string> Messages { get; } = new(StringComparer.Ordinal);
    internal List<string> Servers { get; } = [];

    public IChannelBuilder Name(string id)
    {
        Id = DocumentJson.RequireName(id, "channel ID");
        return this;
    }

    public IChannelBuilder Address(string? address)
    {
        Value["address"] = address;
        return this;
    }

    public IChannelBuilder Description(string description)
    {
        Value["description"] = description;
        return this;
    }

    public IChannelBuilder Message(string messageId, string? key = null)
    {
        var id = DocumentJson.ComponentId(messageId, "message ID");
        var channelKey = DocumentJson.RequireName(key ?? id, "channel message key");

        if (!Messages.TryAdd(channelKey, id))
        {
            throw new InvalidOperationException($"Duplicate AsyncAPI channel message key '{channelKey}'.");
        }

        return this;
    }

    public IChannelBuilder Server(string id)
    {
        var server = DocumentJson.RequireName(id, "server ID");

        if (Servers.Contains(server, StringComparer.Ordinal))
        {
            throw new InvalidOperationException($"Duplicate AsyncAPI channel server '{server}'.");
        }

        Servers.Add(server);
        return this;
    }

    public IChannelBuilder Parameter(string name, JsonObject parameter)
    {
        ArgumentNullException.ThrowIfNull(parameter);

        var key = DocumentJson.RequireName(name, "channel parameter name");
        var parameters = DocumentJson.Object(Value, "parameters");
        DocumentJson.Add(parameters, key, parameter.DeepClone(), "channel parameter");
        return this;
    }

    public IChannelBuilder Bindings(JsonObject bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        Value["bindings"] = bindings.DeepClone();
        return this;
    }

    public IChannelBuilder WithExtension(string name, JsonNode? value)
    {
        DocumentJson.SetExtension(Value, name, value);
        return this;
    }
}
