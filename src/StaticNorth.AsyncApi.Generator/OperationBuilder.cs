using System.Text.Json.Nodes;

namespace StaticNorth.AsyncApi.Generator;

internal sealed class OperationBuilder : IOperationBuilder
{
    internal string? Id { get; private set; }
    internal string? ChannelId { get; private set; }
    internal JsonObject Value { get; } = new();
    internal List<string> Messages { get; } = [];
    internal List<string> Traits { get; } = [];

    public IOperationBuilder Name(string id)
    {
        Id = DocumentJson.RequireName(id, "operation ID");
        return this;
    }

    public IOperationBuilder Channel(string id)
    {
        ChannelId = DocumentJson.RequireName(id, "operation channel ID");
        return this;
    }

    public IOperationBuilder Send()
    {
        SetAction("send");
        return this;
    }

    public IOperationBuilder Receive()
    {
        SetAction("receive");
        return this;
    }

    public IOperationBuilder Description(string description)
    {
        Value["description"] = description;
        return this;
    }

    public IOperationBuilder Message(string channelMessageKey)
    {
        var key = DocumentJson.RequireName(channelMessageKey, "operation message key");

        if (Messages.Contains(key, StringComparer.Ordinal))
        {
            throw new InvalidOperationException($"Duplicate AsyncAPI operation message key '{key}'.");
        }

        Messages.Add(key);
        return this;
    }

    public IOperationBuilder Trait(string id)
    {
        var trait = DocumentJson.ComponentId(id, "operation trait ID");

        if (Traits.Contains(trait, StringComparer.Ordinal))
        {
            throw new InvalidOperationException($"Duplicate AsyncAPI operation trait '{trait}'.");
        }

        Traits.Add(trait);
        return this;
    }

    public IOperationBuilder Bindings(JsonObject bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        Value["bindings"] = bindings.DeepClone();
        return this;
    }

    public IOperationBuilder WithExtension(string name, JsonNode? value)
    {
        DocumentJson.SetExtension(Value, name, value);
        return this;
    }

    private void SetAction(string action)
    {
        if (Value["action"] is JsonValue previous && previous.GetValue<string>() != action)
        {
            throw new InvalidOperationException("An AsyncAPI operation can declare only one action.");
        }

        Value["action"] = action;
    }
}
