using System.Text.Json.Nodes;

namespace StaticNorth.AsyncApi.Generator;

public interface IMessageBuilder<TMessage>
{
    IMessageBuilder<TMessage> Id(string id);
    IMessageBuilder<TMessage> Name(string name);
    IMessageBuilder<TMessage> ContentType(string contentType);
    IMessageBuilder<TMessage> Description(string description);
    IMessageBuilder<TMessage> Header<THeader>(string name, bool required = false);
    IMessageBuilder<TMessage> Headers(JsonObject schema);
    IMessageBuilder<TMessage> CorrelationId(string location, string? description = null);
    IMessageBuilder<TMessage> Example(TMessage payload, JsonObject? headers = null);
    IMessageBuilder<TMessage> PayloadSchema(JsonNode schema);
    IMessageBuilder<TMessage> Bindings(JsonObject bindings);
    IMessageBuilder<TMessage> WithExtension(string name, JsonNode? value);
}
