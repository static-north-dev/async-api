using System.Text.Json.Nodes;

namespace StaticNorth.AsyncApi.Generator;

/// <summary>Configures the document metadata, servers, and reusable components.</summary>
public interface IDocumentBuilder
{
    /// <summary>Sets the required API metadata.</summary>
    /// <param name="title">The name of the API.</param>
    /// <param name="version">The version of the API described by this document.</param>
    /// <returns>This builder.</returns>
    IDocumentBuilder Info(string title, string version);

    /// <summary>Sets the document description.</summary>
    /// <param name="description">The description to include in the document.</param>
    /// <returns>This builder.</returns>
    IDocumentBuilder Description(string description);

    /// <summary>Sets the default media type for messages without an explicit content type.</summary>
    /// <param name="contentType">The media type, such as <c>application/json</c>.</param>
    /// <returns>This builder.</returns>
    IDocumentBuilder DefaultContentType(string contentType);

    /// <summary>Adds a server definition.</summary>
    /// <param name="id">The server ID used by channels to reference this server.</param>
    /// <param name="configure">The callback that configures the server.</param>
    /// <returns>This builder.</returns>
    IDocumentBuilder Server(string id, Action<IServerBuilder> configure);

    /// <summary>Adds a tag to the API metadata.</summary>
    /// <param name="name">The tag name.</param>
    /// <param name="description">An optional description of the tag.</param>
    /// <returns>This builder.</returns>
    IDocumentBuilder Tag(string name, string? description = null);

    /// <summary>Adds a reusable security scheme.</summary>
    /// <param name="id">The component ID of the security scheme.</param>
    /// <param name="scheme">The AsyncAPI Security Scheme Object to copy.</param>
    /// <returns>This builder.</returns>
    IDocumentBuilder SecurityScheme(string id, JsonObject scheme);

    /// <summary>Adds a reusable schema.</summary>
    /// <param name="id">The component ID of the schema.</param>
    /// <param name="schema">The JSON schema object or boolean schema to copy.</param>
    /// <returns>This builder.</returns>
    IDocumentBuilder Schema(string id, JsonNode schema);

    /// <summary>Adds a reusable operation trait.</summary>
    /// <param name="id">The component ID of the trait.</param>
    /// <param name="trait">The AsyncAPI Operation Trait Object to copy.</param>
    /// <returns>This builder.</returns>
    IDocumentBuilder OperationTrait(string id, JsonObject trait);

    /// <summary>Adds or replaces a specification extension on the document.</summary>
    /// <param name="name">The extension name, starting with <c>x-</c>.</param>
    /// <param name="value">The JSON value to copy, or <see langword="null"/> to write JSON null.</param>
    /// <returns>This builder.</returns>
    IDocumentBuilder WithExtension(string name, JsonNode? value);
}
