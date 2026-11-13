using System.Text.Json;

namespace StaticNorth.AsyncApi.Generator.SourceGeneration.Client;

internal readonly struct ContractNode(JsonElement value, string path)
{
    internal JsonElement Value { get; } = value;
    internal string Path { get; } = path;
    internal bool Exists => Value.ValueKind != JsonValueKind.Undefined;
    internal JsonValueKind Kind => Value.ValueKind;

    internal ContractNode Field(string name)
    {
        Require(JsonValueKind.Object);
        Value.TryGetProperty(name, out var field);
        return new ContractNode(field, Path + "/" + Escape(name));
    }

    internal string String()
    {
        Require(JsonValueKind.String);
        return Value.GetString()!;
    }

    internal IEnumerable<(string Name, ContractNode Node)> Fields()
    {
        Require(JsonValueKind.Object);
        var fields = Value
            .EnumerateObject()
            .OrderBy(property => property.Name, StringComparer.Ordinal);

        foreach (var field in fields)
        {
            yield return (field.Name, new ContractNode(field.Value, Path + "/" + Escape(field.Name)));
        }
    }

    internal IEnumerable<ContractNode> Items()
    {
        Require(JsonValueKind.Array);
        var index = 0;

        foreach (var item in Value.EnumerateArray())
        {
            yield return new ContractNode(item, Path + "/" + index.ToString(System.Globalization.CultureInfo.InvariantCulture));
            index++;
        }
    }

    internal void Require(JsonValueKind kind)
    {
        if (Kind != kind)
        {
            throw new ContractException(Path, $"expected {kind}, found {Kind}");
        }
    }

    private static string Escape(string value)
    {
        var pointer = value
            .Replace("~", "~0")
            .Replace("/", "~1");
        return Uri.EscapeDataString(pointer);
    }
}
