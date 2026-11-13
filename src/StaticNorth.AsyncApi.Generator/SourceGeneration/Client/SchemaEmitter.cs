using System.Text;
using System.Text.Json;

namespace StaticNorth.AsyncApi.Generator.SourceGeneration.Client;

internal sealed class SchemaEmitter(ContractReferences references, HashSet<string> symbols)
{
    private static readonly HashSet<string> SupportedKeywords = new(StringComparer.Ordinal)
    {
        "$ref", "$comment", "type", "format", "properties", "required", "items", "enum", "anyOf", "oneOf",
        "additionalProperties", "definitions", "$defs", "title", "description", "default", "examples", "deprecated",
        "minimum", "maximum", "exclusiveMinimum", "exclusiveMaximum", "multipleOf", "minLength", "maxLength",
        "pattern", "minItems", "maxItems", "uniqueItems", "minProperties", "maxProperties"
    };

    private readonly Dictionary<string, string> _names = new(StringComparer.Ordinal);
    private readonly HashSet<string> _active = new(StringComparer.Ordinal);
    private readonly SortedDictionary<string, string> _declarations = new(StringComparer.Ordinal);

    internal IEnumerable<string> Declarations => _declarations.Values;

    internal string TypeOf(ContractNode schema, string suggestedName, int depth = 0)
    {
        if (depth > 128)
        {
            throw Unsupported(schema, "schema nesting exceeds the generation limit");
        }

        var (resolved, unionNullable) = ResolveSchema(schema);
        schema = resolved;
        var type = schema.Field("type");
        var enumeration = schema.Field("enum");

        if (!_active.Add(schema.Path))
        {
            if (_names.TryGetValue(schema.Path, out var model))
            {
                return WithNullability(model, unionNullable || ReadType(type).Nullable);
            }

            throw Unsupported(schema, "recursive references must pass through an object model");
        }

        try
        {
            var (kind, nullable) = ReadType(type);
            var format = schema.Field("format");
            var formatName = format.Exists ? format.String() : null;
            ValidateShape(schema, kind);

            if (enumeration.Exists)
            {
                if (kind is not (null or "string") || format.Exists)
                {
                    throw Unsupported(enumeration, "only string enums are supported");
                }

                var enumType = EmitEnum(schema, enumeration, suggestedName, kind is null || nullable);
                return WithNullability(enumType, unionNullable);
            }

            var mapped = kind switch
            {
                "string" => formatName switch
                {
                    "uuid" => "global::System.Guid",
                    "date-time" => "global::System.DateTimeOffset",
                    "date" => "global::System.DateOnly",
                    "time" => "global::System.TimeOnly",
                    "byte" => "byte[]",
                    _ => "string"
                },
                "boolean" => "bool",
                "integer" => formatName == "int32" ? "int" : "long",
                "number" => formatName switch { "float" => "float", "double" => "double", _ => "decimal" },
                "array" => TypeOf(schema.Field("items"), suggestedName + "Item", depth + 1) + "[]",
                "object" => EmitObject(schema, suggestedName, depth),
                _ => throw Unsupported(schema, "a supported JSON Schema type or string enum is required")
            };

            return WithNullability(mapped, unionNullable || nullable);
        }
        finally
        {
            _active.Remove(schema.Path);
        }
    }

    private (ContractNode Schema, bool Nullable) ResolveSchema(ContractNode schema)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var nullable = false;

        while (true)
        {
            if (schema.Kind is JsonValueKind.True or JsonValueKind.False)
            {
                throw Unsupported(schema, "boolean schemas cannot be mapped to a DTO");
            }

            schema.Require(JsonValueKind.Object);
            if (!seen.Add(schema.Path))
            {
                throw Unsupported(schema, "cyclic schema aliases must pass through an object model");
            }

            if (seen.Count > 128)
            {
                throw Unsupported(schema, "schema alias chain exceeds the generation limit");
            }

            var format = schema.Field("schemaFormat");

            if (format.Exists)
            {
                if (format.String() is not ("application/schema+json;version=draft-07" or
                    "application/vnd.aai.asyncapi;version=3.1.0" or "application/vnd.aai.asyncapi+json;version=3.1.0"))
                {
                    throw Unsupported(format, "unsupported payload schema format");
                }

                schema = schema.Field("schema");
                continue;
            }

            ValidateKeywords(schema);
            var reference = schema.Field("$ref");
            var anyOf = schema.Field("anyOf");
            var oneOf = schema.Field("oneOf");
            var union = anyOf.Exists ? anyOf : oneOf;

            if (!reference.Exists && !union.Exists)
            {
                return (schema, nullable);
            }

            var keyword = reference.Exists ? "$ref" : anyOf.Exists ? "anyOf" : "oneOf";

            foreach (var (name, field) in schema.Fields())
            {
                if (name != keyword && name is not ("$comment" or "title" or "description" or "default" or "examples" or "deprecated") &&
                    !name.StartsWith("x-", StringComparison.Ordinal))
                {
                    throw Unsupported(field, $"schema constraints alongside {keyword} are unsupported");
                }
            }

            if (reference.Exists)
            {
                schema = references.Resolve(reference);
            }
            else
            {
                schema = NullableUnionMember(union);
                nullable = true;
            }
        }
    }

    private static ContractNode NullableUnionMember(ContractNode union)
    {
        var items = union
            .Items()
            .ToArray();
        var nonNull = items
            .Where(item => !IsNullSchema(item))
            .ToArray();

        if (items.Length != 2 || nonNull.Length != 1)
        {
            throw Unsupported(union, "only a union of one supported type and null is supported");
        }

        return nonNull[0];
    }

    private string EmitEnum(ContractNode schema, ContractNode enumeration, string name, bool canIncludeNull)
    {
        if (_names.TryGetValue(schema.Path, out var existing))
        {
            var nullable = enumeration
                .Items()
                .Any(item => item.Kind == JsonValueKind.Null);
            return WithNullability(existing, nullable);
        }

        var values = enumeration
            .Items()
            .ToArray();
        var strings = values
            .Where(item => item.Kind == JsonValueKind.String)
            .ToArray();
        var nulls = values.Count(item => item.Kind == JsonValueKind.Null);

        if (strings.Length == 0 || values.Length != strings.Length + nulls || nulls > 1 || (!canIncludeNull && nulls != 0))
        {
            throw Unsupported(enumeration, "only string enum values with optional null are supported");
        }

        Reserve(schema, name);
        var members = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { name, "Value__" };
        var wireNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var source = new StringBuilder();
        source
            .Append("/// <summary>Represents a string enumeration declared in the AsyncAPI payload schema.</summary>\n")
            .Append($"[global::System.Text.Json.Serialization.JsonConverter(typeof(global::System.Text.Json.Serialization.JsonStringEnumConverter<{name}>))]\n")
            .Append($"public enum {name}\n{{\n");

        foreach (var item in strings)
        {
            var wireName = item.String();
            var member = ClientNames.Identifier(wireName);

            if (string.IsNullOrWhiteSpace(wireName) || wireName.Trim() != wireName || wireName.Contains(',') ||
                !wireNames.Add(wireName) || !members.Add(member))
            {
                throw Unsupported(item, $"enum value '{wireName}' cannot be represented unambiguously in '{name}'");
            }

            source
                .Append($"    /// <summary>The wire value <c>{ClientNames.XmlText(wireName)}</c>.</summary>\n")
                .Append($"    [global::System.Text.Json.Serialization.JsonStringEnumMemberName({ClientNames.Literal(wireName)})]\n")
                .Append($"    {member},\n");
        }

        source.Append("}\n");
        _declarations.Add(name, source.ToString());
        return WithNullability(name, nulls != 0);
    }

    private string EmitObject(ContractNode schema, string name, int depth)
    {
        if (_names.TryGetValue(schema.Path, out var existing))
        {
            return existing;
        }

        var properties = schema.Field("properties");
        var required = schema.Field("required");
        var additional = schema.Field("additionalProperties");
        var fields = properties.Exists
            ? properties
                .Fields()
                .ToArray()
            : [];
        var requiredNames = new HashSet<string>(StringComparer.Ordinal);

        if (required.Exists)
        {
            foreach (var item in required.Items())
            {
                var key = item.String();

                if (!requiredNames.Add(key) || !fields.Any(field => field.Name == key))
                {
                    throw Unsupported(item, $"required property '{key}' must appear exactly once in properties");
                }
            }
        }

        if (additional.Kind == JsonValueKind.Object)
        {
            if (fields.Length != 0)
            {
                throw Unsupported(additional, "typed additionalProperties cannot be combined with named fields");
            }

            var valueType = TypeOf(additional, name + "Value", depth + 1);
            return $"global::System.Collections.Generic.Dictionary<string, {valueType}>";
        }

        if (additional.Exists && additional.Kind is not (JsonValueKind.True or JsonValueKind.False))
        {
            throw Unsupported(additional, "additionalProperties must be a schema object or boolean");
        }

        if (fields.Length == 0 && additional.Kind != JsonValueKind.False)
        {
            throw Unsupported(schema, "open objects without properties require a typed additionalProperties schema");
        }

        Reserve(schema, name);
        var members = new HashSet<string>(StringComparer.Ordinal) { name, "Equals", "GetHashCode", "GetType", "ToString", "MemberwiseClone", "Finalize" };
        var source = new StringBuilder();
        source.Append("/// <summary>Represents an object declared in the AsyncAPI payload schema.</summary>\n");
        if (additional.Kind == JsonValueKind.False)
        {
            source.Append("[global::System.Text.Json.Serialization.JsonUnmappedMemberHandling(global::System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow)]\n");
        }

        source.Append($"public sealed partial class {name}\n{{\n");

        foreach (var (wireName, field) in fields)
        {
            var member = ClientNames.Identifier(wireName);

            if (!members.Add(member))
            {
                throw new ContractException(field.Path, $"property '{wireName}' conflicts with another member of '{name}'");
            }

            var fieldType = TypeOf(field, name + member, depth + 1);
            var isRequired = requiredNames.Contains(wireName);
            var requiredness = isRequired ? "required" : "optional";
            source
                .Append($"    /// <summary>Gets the {requiredness} JSON property <c>{ClientNames.XmlText(wireName)}</c>.</summary>\n")
                .Append($"    [global::System.Text.Json.Serialization.JsonPropertyName({ClientNames.Literal(wireName)})]\n");

            if (!isRequired)
            {
                source.Append("    [global::System.Text.Json.Serialization.JsonIgnore(Condition = global::System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]\n");

                if (!fieldType.EndsWith("?", StringComparison.Ordinal))
                {
                    source.Append("    [global::System.Diagnostics.CodeAnalysis.DisallowNull]\n");
                }
            }

            var modifier = isRequired ? "required " : "";
            source.Append($"    public {modifier}{WithNullability(fieldType, !isRequired)} {member} {{ get; init; }}\n\n");
        }

        source.Append("}\n");
        _declarations.Add(name, source.ToString());
        return name;
    }

    private void Reserve(ContractNode schema, string name)
    {
        if (!symbols.Add(name))
        {
            throw new ContractException(schema.Path, $"generated symbol '{name}' conflicts with another declaration");
        }

        _names.Add(schema.Path, name);
    }

    private static (string? Type, bool Nullable) ReadType(ContractNode type)
    {
        if (!type.Exists || type.Kind == JsonValueKind.String)
        {
            return (type.Exists ? type.String() : null, false);
        }

        var names = type
            .Items()
            .Select(item => item.String())
            .ToArray();
        var nonNull = names
            .Where(name => name != "null")
            .ToArray();

        if (names.Length is < 1 or > 2 || nonNull.Length != 1)
        {
            throw Unsupported(type, "only a single type with optional null is supported");
        }

        return (nonNull[0], names.Length == 2);
    }

    private static bool IsNullSchema(ContractNode schema)
    {
        if (schema.Kind != JsonValueKind.Object || !schema.Value.TryGetProperty("type", out var type) ||
            type.ValueKind != JsonValueKind.String || type.GetString() != "null")
        {
            return false;
        }

        return schema
            .Fields()
            .All(field => field.Name is "type" or "title" or "description" or "$comment");
    }

    private static void ValidateKeywords(ContractNode schema)
    {
        foreach (var (name, field) in schema.Fields())
        {
            if (!SupportedKeywords.Contains(name) && !name.StartsWith("x-", StringComparison.Ordinal))
            {
                throw Unsupported(field, $"schema keyword '{name}' is unsupported");
            }
        }
    }

    private static void ValidateShape(ContractNode schema, string? kind)
    {
        if (kind != "object" && (schema.Field("properties").Exists || schema.Field("required").Exists ||
            schema.Field("additionalProperties").Exists))
        {
            throw Unsupported(schema, "object fields require an explicit object type");
        }

        if (kind != "array" && schema.Field("items").Exists)
        {
            throw Unsupported(schema, "items requires an explicit array type");
        }
    }

    private static string WithNullability(string type, bool nullable)
    {
        return nullable && !type.EndsWith("?", StringComparison.Ordinal) ? type + "?" : type;
    }

    private static ContractException Unsupported(ContractNode node, string message) => new(node.Path, message, unsupported: true);
}
