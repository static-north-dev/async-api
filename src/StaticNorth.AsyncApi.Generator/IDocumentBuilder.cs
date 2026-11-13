using System.Text.Json.Nodes;

namespace StaticNorth.AsyncApi.Generator;

public interface IDocumentBuilder
{
    IDocumentBuilder Info(string title, string version);
    IDocumentBuilder Description(string description);
    IDocumentBuilder DefaultContentType(string contentType);
    IDocumentBuilder Server(string id, Action<IServerBuilder> configure);
    IDocumentBuilder Tag(string name, string? description = null);
    IDocumentBuilder SecurityScheme(string id, JsonObject scheme);
    IDocumentBuilder Schema(string id, JsonNode schema);
    IDocumentBuilder OperationTrait(string id, JsonObject trait);
    IDocumentBuilder WithExtension(string name, JsonNode? value);
}
