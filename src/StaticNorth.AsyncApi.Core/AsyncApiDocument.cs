using System.Text.Json;

namespace StaticNorth.AsyncApi.Core;

public sealed class AsyncApiDocument
{
    internal AsyncApiDocument(string specificationVersion, AsyncApiInfo info, JsonElement root)
    {
        SpecificationVersion = specificationVersion;
        Info = info;
        Root = root;
    }

    public string SpecificationVersion { get; }

    public AsyncApiInfo Info { get; }

    public JsonElement Root { get; }

    public JsonElement? Servers => GetSection("servers");

    public JsonElement? Channels => GetSection("channels");

    public JsonElement? Operations => GetSection("operations");

    public JsonElement? Components => GetSection("components");

    private JsonElement? GetSection(string name)
    {
        return Root.TryGetProperty(name, out var section) ? section : null;
    }
}
