using System.Text.Json;

namespace StaticNorth.AsyncApi.Core;

/// <summary>Represents an AsyncAPI document with typed metadata and access to its original JSON structure.</summary>
public sealed class AsyncApiDocument
{
    internal AsyncApiDocument(string specificationVersion, AsyncApiInfo info, JsonElement root)
    {
        SpecificationVersion = specificationVersion;
        Info = info;
        Root = root;
    }

    /// <summary>Gets the version of the AsyncAPI specification used by this document.</summary>
    public string SpecificationVersion { get; }

    /// <summary>Gets the title and version of the API described by the document.</summary>
    public AsyncApiInfo Info { get; }

    /// <summary>Gets the complete JSON object, including fields without typed accessors.</summary>
    public JsonElement Root { get; }

    /// <summary>Gets the server definitions, or <see langword="null"/> if the section is absent.</summary>
    public JsonElement? Servers => GetSection("servers");

    /// <summary>Gets the channel definitions, or <see langword="null"/> if the section is absent.</summary>
    public JsonElement? Channels => GetSection("channels");

    /// <summary>Gets the operation definitions, or <see langword="null"/> if the section is absent.</summary>
    public JsonElement? Operations => GetSection("operations");

    /// <summary>Gets the reusable components, or <see langword="null"/> if the section is absent.</summary>
    public JsonElement? Components => GetSection("components");

    private JsonElement? GetSection(string name)
    {
        return Root.TryGetProperty(name, out var section) ? section : null;
    }
}
