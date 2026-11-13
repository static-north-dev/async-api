using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using StaticNorth.AsyncApi.Core;

namespace StaticNorth.AsyncApi.Generator;

public sealed class AsyncApiDocumentBuilder
{
    private readonly Action<IDocumentBuilder> _configure;
    private readonly JsonSerializerOptions _serializerOptions;
    private readonly List<Action<JsonObject>> _messages = [];
    private readonly List<Func<IAsyncApiChannelDefinition>> _channels = [];
    private readonly List<Func<IAsyncApiOperationDefinition>> _operations = [];

    public AsyncApiDocumentBuilder(Action<IDocumentBuilder> configure, JsonSerializerOptions? serializerOptions = null)
    {
        ArgumentNullException.ThrowIfNull(configure);
        _configure = configure;
        _serializerOptions = serializerOptions is null
            ? new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                RespectNullableAnnotations = true,
                RespectRequiredConstructorParameters = true
            }
            : new JsonSerializerOptions(serializerOptions);
        _serializerOptions.TypeInfoResolver ??= new DefaultJsonTypeInfoResolver();
        _serializerOptions.MakeReadOnly();
    }

    public AsyncApiDocumentBuilder AddMessage<TMessage>(Func<IAsyncApiMessageDefinition<TMessage>> create)
    {
        ArgumentNullException.ThrowIfNull(create);
        _messages.Add(components =>
        {
            var definition = CreateDefinition(create);
            var message = new MessageBuilder<TMessage>(_serializerOptions);
            definition.Configure(message);
            message.AddTo(components);
        });
        return this;
    }

    public AsyncApiDocumentBuilder AddChannel(Func<IAsyncApiChannelDefinition> create)
    {
        ArgumentNullException.ThrowIfNull(create);
        _channels.Add(create);
        return this;
    }

    public AsyncApiDocumentBuilder AddOperation(Func<IAsyncApiOperationDefinition> create)
    {
        ArgumentNullException.ThrowIfNull(create);
        _operations.Add(create);
        return this;
    }

    public AsyncApiDocument Build()
    {
        var document = new DocumentBuilder();
        var root = document.Value;
        root.Add("asyncapi", AsyncApiJson.SupportedVersion);
        _configure(document);
        DocumentJson.RequireName((string?)root["info"]?["title"], "info.title");
        DocumentJson.RequireName((string?)root["info"]?["version"], "info.version");

        foreach (var message in _messages)
        {
            message(DocumentJson.Object(root, "components"));
        }

        var channels = new Dictionary<string, ChannelBuilder>(StringComparer.Ordinal);

        foreach (var factory in _channels)
        {
            var definition = CreateDefinition(factory);
            var channel = new ChannelBuilder();
            definition.Configure(channel);
            var id = DocumentJson.RequireName(channel.Id, "channel ID");
            var value = BuildChannel(root, id, channel);
            DocumentJson.Add(DocumentJson.Object(root, "channels"), id, value, "channel ID");
            channels.Add(id, channel);
        }

        foreach (var factory in _operations)
        {
            var definition = CreateDefinition(factory);
            var operation = new OperationBuilder();
            definition.Configure(operation);
            var id = DocumentJson.RequireName(operation.Id, "operation ID");
            var value = BuildOperation(root, channels, id, operation);
            DocumentJson.Add(DocumentJson.Object(root, "operations"), id, value, "operation ID");
        }

        return AsyncApiJson.Read(JsonSerializer.SerializeToUtf8Bytes(root));
    }

    private static JsonObject BuildChannel(JsonObject root, string id, ChannelBuilder channel)
    {
        var value = (JsonObject)channel.Value.DeepClone();
        ValidateAddress(id, (string?)value["address"], value["parameters"] as JsonObject);

        foreach (var (key, messageId) in channel.Messages)
        {
            if (root["components"]?["messages"]?[messageId] is null)
            {
                throw new InvalidOperationException($"AsyncAPI channel '{id}' references unknown message '{messageId}'.");
            }

            var messages = DocumentJson.Object(value, "messages");
            messages.Add(key, DocumentJson.Reference($"#/components/messages/{DocumentJson.PointerToken(messageId)}"));
        }

        foreach (var serverId in channel.Servers)
        {
            if (root["servers"]?[serverId] is null)
            {
                throw new InvalidOperationException($"AsyncAPI channel '{id}' references unknown server '{serverId}'.");
            }

            var servers = DocumentJson.Array(value, "servers");
            servers.Add(DocumentJson.Reference($"#/servers/{DocumentJson.PointerToken(serverId)}"));
        }

        return value;
    }

    private static void ValidateAddress(string id, string? address, JsonObject? parameters)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        var remaining = address.AsSpan();
        var brace = remaining.IndexOfAny('{', '}');

        while (brace >= 0)
        {
            if (remaining[brace] != '{')
            {
                throw new InvalidOperationException($"AsyncAPI channel '{id}' has an invalid address expression.");
            }

            remaining = remaining[(brace + 1)..];
            var end = remaining.IndexOfAny('{', '}');

            if (end <= 0 || remaining[end] != '}')
            {
                throw new InvalidOperationException($"AsyncAPI channel '{id}' has an invalid address expression.");
            }

            names.Add(remaining[..end].ToString());
            remaining = remaining[(end + 1)..];
            brace = remaining.IndexOfAny('{', '}');
        }

        if (names.Count != (parameters?.Count ?? 0) ||
            (parameters is not null && parameters.Any(parameter => !names.Contains(parameter.Key))))
        {
            throw new InvalidOperationException($"AsyncAPI channel '{id}' must declare exactly the parameters used in its address.");
        }
    }

    private static JsonObject BuildOperation(
        JsonObject root,
        Dictionary<string, ChannelBuilder> channels,
        string id,
        OperationBuilder operation)
    {
        var channelId = DocumentJson.RequireName(operation.ChannelId, $"operation '{id}' channel ID");

        if (!channels.TryGetValue(channelId, out var channel))
        {
            throw new InvalidOperationException($"AsyncAPI operation '{id}' references unknown channel '{channelId}'.");
        }

        var value = (JsonObject)operation.Value.DeepClone();
        DocumentJson.RequireName((string?)value["action"], $"operation '{id}' action");
        var pointer = $"#/channels/{DocumentJson.PointerToken(channelId)}";
        value.Add("channel", DocumentJson.Reference(pointer));

        foreach (var key in operation.Messages)
        {
            if (!channel.Messages.ContainsKey(key))
            {
                throw new InvalidOperationException($"AsyncAPI operation '{id}' references message key '{key}', which is absent from channel '{channelId}'.");
            }

            var messages = DocumentJson.Array(value, "messages");
            messages.Add(DocumentJson.Reference($"{pointer}/messages/{DocumentJson.PointerToken(key)}"));
        }

        foreach (var traitId in operation.Traits)
        {
            if (root["components"]?["operationTraits"]?[traitId] is null)
            {
                throw new InvalidOperationException($"AsyncAPI operation '{id}' references unknown trait '{traitId}'.");
            }

            var traits = DocumentJson.Array(value, "traits");
            traits.Add(DocumentJson.Reference($"#/components/operationTraits/{DocumentJson.PointerToken(traitId)}"));
        }

        return value;
    }

    private static T CreateDefinition<T>(Func<T> factory) where T : class
    {
        return factory() ?? throw new InvalidOperationException($"An AsyncAPI factory for '{typeof(T)}' returned null.");
    }
}
