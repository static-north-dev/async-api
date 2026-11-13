using System.Text.Json.Nodes;

namespace StaticNorth.AsyncApi.Generator;

/// <summary>Configures a reusable message and its payload, headers, and examples.</summary>
/// <typeparam name="TMessage">The payload type used for schema inference and examples.</typeparam>
public interface IMessageBuilder<TMessage>
{
    /// <summary>Sets the reusable message component ID.</summary>
    /// <param name="id">The ID that channels use to reference this message. If omitted, the message name is used.</param>
    /// <returns>This builder.</returns>
    IMessageBuilder<TMessage> Id(string id);

    /// <summary>Sets the message name recorded in the contract.</summary>
    /// <param name="name">The message name. It also serves as the component ID unless an explicit ID is set.</param>
    /// <returns>This builder.</returns>
    IMessageBuilder<TMessage> Name(string name);

    /// <summary>Sets the media type of the message payload.</summary>
    /// <param name="contentType">The payload media type, such as <c>application/json</c>.</param>
    /// <returns>This builder.</returns>
    IMessageBuilder<TMessage> ContentType(string contentType);

    /// <summary>Sets the message description.</summary>
    /// <param name="description">The description to include in the document.</param>
    /// <returns>This builder.</returns>
    IMessageBuilder<TMessage> Description(string description);

    /// <summary>Adds a header whose schema is inferred from its .NET type.</summary>
    /// <typeparam name="THeader">The type used to infer the header schema.</typeparam>
    /// <param name="name">The header property name.</param>
    /// <param name="required">Whether the header must be present.</param>
    /// <returns>This builder.</returns>
    IMessageBuilder<TMessage> Header<THeader>(string name, bool required = false);

    /// <summary>Sets an explicit schema for the message headers.</summary>
    /// <param name="schema">The headers schema to copy. Use this instead of individual typed headers.</param>
    /// <returns>This builder.</returns>
    IMessageBuilder<TMessage> Headers(JsonObject schema);

    /// <summary>Declares where to find the correlation ID in a message.</summary>
    /// <param name="location">An AsyncAPI runtime expression starting with <c>$message.header#/</c> or <c>$message.payload#/</c>.</param>
    /// <param name="description">An optional description of the correlation ID.</param>
    /// <returns>This builder.</returns>
    IMessageBuilder<TMessage> CorrelationId(string location, string? description = null);

    /// <summary>Adds a message example using the configured JSON serializer.</summary>
    /// <param name="payload">The example payload.</param>
    /// <param name="headers">Optional example header values.</param>
    /// <returns>This builder.</returns>
    IMessageBuilder<TMessage> Example(TMessage payload, JsonObject? headers = null);

    /// <summary>Sets an explicit payload schema in place of schema inference.</summary>
    /// <param name="schema">The JSON schema object or boolean schema to copy.</param>
    /// <returns>This builder.</returns>
    IMessageBuilder<TMessage> PayloadSchema(JsonNode schema);

    /// <summary>Sets the protocol bindings for the message.</summary>
    /// <param name="bindings">An object keyed by protocol name. The builder copies it.</param>
    /// <returns>This builder.</returns>
    IMessageBuilder<TMessage> Bindings(JsonObject bindings);

    /// <summary>Adds or replaces a specification extension on the message.</summary>
    /// <param name="name">The extension name, starting with <c>x-</c>.</param>
    /// <param name="value">The JSON value to copy, or <see langword="null"/> to write JSON null.</param>
    /// <returns>This builder.</returns>
    IMessageBuilder<TMessage> WithExtension(string name, JsonNode? value);
}
