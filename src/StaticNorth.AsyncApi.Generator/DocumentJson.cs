using System.Buffers;
using System.Text.Json.Nodes;

namespace StaticNorth.AsyncApi.Generator;

internal static class DocumentJson
{
    private static readonly SearchValues<char> ComponentCharacters =
        SearchValues.Create("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789.-_");

    public static string RequireName(string? name, string field)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException($"AsyncAPI {field} is required.");
        }

        return name;
    }

    public static string ComponentId(string? id, string field)
    {
        var name = RequireName(id, field);

        if (name.AsSpan().ContainsAnyExcept(ComponentCharacters))
        {
            throw new InvalidOperationException($"AsyncAPI {field} '{name}' must use only ASCII letters, digits, '.', '-' or '_'.");
        }

        return name;
    }

    public static void Add(JsonObject map, string key, JsonNode value, string field)
    {
        if (!map.TryAdd(key, value))
        {
            throw new InvalidOperationException($"Duplicate AsyncAPI {field} '{key}'.");
        }
    }

    public static JsonObject Object(JsonObject parent, string name)
    {
        if (parent[name] is JsonObject existing)
        {
            return existing;
        }

        var value = new JsonObject();
        parent.Add(name, value);
        return value;
    }

    public static JsonArray Array(JsonObject parent, string name)
    {
        if (parent[name] is JsonArray existing)
        {
            return existing;
        }

        var value = new JsonArray();
        parent.Add(name, value);
        return value;
    }

    public static void SetExtension(JsonObject node, string name, JsonNode? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (!name.StartsWith("x-", StringComparison.Ordinal) || name.Length == 2 ||
            name.AsSpan(2).ContainsAnyExcept(ComponentCharacters))
        {
            throw new ArgumentException("Use an x- extension name containing letters, digits, '.', '-' or '_'.", nameof(name));
        }

        node[name] = value?.DeepClone();
    }

    public static JsonObject Reference(string pointer) => new() { ["$ref"] = pointer };

    public static string PointerToken(string value)
    {
        var escaped = value
            .Replace("~", "~0", StringComparison.Ordinal)
            .Replace("/", "~1", StringComparison.Ordinal);
        return Uri.EscapeDataString(escaped);
    }
}
