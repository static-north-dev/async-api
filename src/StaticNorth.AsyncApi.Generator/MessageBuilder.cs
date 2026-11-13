using System.Text.Json;
using System.Text.Json.Nodes;

namespace StaticNorth.AsyncApi.Generator;

internal sealed class MessageBuilder<TMessage>(JsonSerializerOptions serializerOptions) : IMessageBuilder<TMessage>
{
    private readonly Dictionary<string, Type> _headers = new(StringComparer.Ordinal);
    private readonly List<string> _requiredHeaders = [];
    private string? _id;
    private JsonNode? _payloadSchema;
    private JsonObject? _headersSchema;

    private JsonObject Value { get; } = new();

    public IMessageBuilder<TMessage> Id(string id)
    {
        _id = DocumentJson.ComponentId(id, "message ID");
        return this;
    }

    public IMessageBuilder<TMessage> Name(string name)
    {
        Value["name"] = DocumentJson.RequireName(name, "message name");
        return this;
    }

    public IMessageBuilder<TMessage> ContentType(string contentType)
    {
        Value["contentType"] = DocumentJson.RequireName(contentType, "message content type");
        return this;
    }

    public IMessageBuilder<TMessage> Description(string description)
    {
        Value["description"] = description;
        return this;
    }

    public IMessageBuilder<TMessage> Header<THeader>(string name, bool required = false)
    {
        var key = DocumentJson.RequireName(name, "header name");

        if (_headersSchema is not null || !_headers.TryAdd(key, typeof(THeader)))
        {
            throw new InvalidOperationException($"AsyncAPI header '{key}' conflicts with an existing header declaration.");
        }

        if (required)
        {
            _requiredHeaders.Add(key);
        }

        return this;
    }

    public IMessageBuilder<TMessage> Headers(JsonObject schema)
    {
        ArgumentNullException.ThrowIfNull(schema);

        if (_headers.Count != 0)
        {
            throw new InvalidOperationException("An explicit AsyncAPI headers schema cannot be combined with typed headers.");
        }

        _headersSchema = (JsonObject)schema.DeepClone();
        return this;
    }

    public IMessageBuilder<TMessage> CorrelationId(string location, string? description = null)
    {
        var expression = DocumentJson.RequireName(location, "correlation ID location");

        if (!expression.StartsWith("$message.header#/", StringComparison.Ordinal) &&
            !expression.StartsWith("$message.payload#/", StringComparison.Ordinal))
        {
            throw new ArgumentException("Use an AsyncAPI runtime expression pointing to a message header or payload field.", nameof(location));
        }

        var correlationId = new JsonObject { ["location"] = expression };

        if (description is not null)
        {
            correlationId["description"] = description;
        }

        Value["correlationId"] = correlationId;
        return this;
    }

    public IMessageBuilder<TMessage> Example(TMessage payload, JsonObject? headers = null)
    {
        var example = new JsonObject { ["payload"] = JsonSerializer.SerializeToNode(payload, serializerOptions) };

        if (headers is not null)
        {
            example["headers"] = headers.DeepClone();
        }

        DocumentJson.Array(Value, "examples").Add(example);
        return this;
    }

    public IMessageBuilder<TMessage> PayloadSchema(JsonNode schema)
    {
        PayloadSchemaExporter.ValidateSchema(schema);
        _payloadSchema = schema.DeepClone();
        return this;
    }

    public IMessageBuilder<TMessage> Bindings(JsonObject bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        Value["bindings"] = bindings.DeepClone();
        return this;
    }

    public IMessageBuilder<TMessage> WithExtension(string name, JsonNode? value)
    {
        DocumentJson.SetExtension(Value, name, value);
        return this;
    }

    internal void AddTo(JsonObject components)
    {
        var id = DocumentJson.ComponentId(_id ?? (string?)Value["name"], "message ID or name");
        var message = (JsonObject)Value.DeepClone();
        var messages = DocumentJson.Object(components, "messages");
        DocumentJson.Add(messages, id, message, "message ID");

        if (_payloadSchema is null)
        {
            var pointer = $"#/components/schemas/{DocumentJson.PointerToken(id)}";
            var schema = PayloadSchemaExporter.Export(serializerOptions, typeof(TMessage), pointer);
            var schemas = DocumentJson.Object(components, "schemas");
            DocumentJson.Add(schemas, id, schema, "schema component ID");
            message["payload"] = DocumentJson.Reference(pointer);
        }
        else
        {
            message["payload"] = _payloadSchema.DeepClone();
        }

        var headers = BuildHeaders(id);

        if (headers is not null)
        {
            message["headers"] = headers;
        }
    }

    private JsonObject? BuildHeaders(string id)
    {
        if (_headersSchema is not null)
        {
            return (JsonObject)_headersSchema.DeepClone();
        }

        if (_headers.Count == 0)
        {
            return null;
        }

        var properties = new JsonObject();
        var headers = new JsonObject { ["type"] = "object", ["properties"] = properties };

        foreach (var (name, type) in _headers)
        {
            var pointer = $"#/components/messages/{DocumentJson.PointerToken(id)}/headers/properties/{DocumentJson.PointerToken(name)}";
            properties.Add(name, PayloadSchemaExporter.Export(serializerOptions, type, pointer));
        }

        if (_requiredHeaders.Count != 0)
        {
            headers["required"] = JsonSerializer.SerializeToNode(_requiredHeaders);
        }

        return headers;
    }
}
