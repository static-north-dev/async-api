using System.Text.Json.Nodes;

namespace StaticNorth.AsyncApi.Generator;

internal sealed class DocumentBuilder : IDocumentBuilder
{
    internal JsonObject Value { get; } = new();

    public IDocumentBuilder Info(string title, string version)
    {
        var info = DocumentJson.Object(Value, "info");
        info["title"] = DocumentJson.RequireName(title, "info.title");
        info["version"] = DocumentJson.RequireName(version, "info.version");
        return this;
    }

    public IDocumentBuilder Description(string description)
    {
        DocumentJson.Object(Value, "info")["description"] = description;
        return this;
    }

    public IDocumentBuilder DefaultContentType(string contentType)
    {
        Value["defaultContentType"] = DocumentJson.RequireName(contentType, "default content type");
        return this;
    }

    public IDocumentBuilder Server(string id, Action<IServerBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var serverId = DocumentJson.RequireName(id, "server ID");
        var server = new ServerBuilder();
        configure(server);
        DocumentJson.RequireName((string?)server.Value["host"], $"server '{serverId}' host");
        DocumentJson.RequireName((string?)server.Value["protocol"], $"server '{serverId}' protocol");
        var servers = DocumentJson.Object(Value, "servers");
        DocumentJson.Add(servers, serverId, server.Value.DeepClone(), "server ID");
        return this;
    }

    public IDocumentBuilder Tag(string name, string? description = null)
    {
        var info = DocumentJson.Object(Value, "info");
        var tags = DocumentJson.Array(info, "tags");
        var tagName = DocumentJson.RequireName(name, "tag name");

        if (tags.Any(tag => (string?)tag?["name"] == tagName))
        {
            throw new InvalidOperationException($"Duplicate AsyncAPI tag '{tagName}'.");
        }

        var tag = new JsonObject { ["name"] = tagName };

        if (description is not null)
        {
            tag["description"] = description;
        }

        tags.Add(tag);
        return this;
    }

    public IDocumentBuilder SecurityScheme(string id, JsonObject scheme)
    {
        AddComponent("securitySchemes", id, scheme);
        return this;
    }

    public IDocumentBuilder Schema(string id, JsonNode schema)
    {
        PayloadSchemaExporter.ValidateSchema(schema);
        AddComponent("schemas", id, schema);
        return this;
    }

    public IDocumentBuilder OperationTrait(string id, JsonObject trait)
    {
        AddComponent("operationTraits", id, trait);
        return this;
    }

    public IDocumentBuilder WithExtension(string name, JsonNode? value)
    {
        DocumentJson.SetExtension(Value, name, value);
        return this;
    }

    private void AddComponent(string section, string id, JsonNode value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var componentId = DocumentJson.ComponentId(id, $"{section} component ID");
        var components = DocumentJson.Object(Value, "components");
        var map = DocumentJson.Object(components, section);
        DocumentJson.Add(map, componentId, value.DeepClone(), $"{section} component ID");
    }
}
