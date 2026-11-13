using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;

namespace StaticNorth.AsyncApi.Generator;

internal static class PayloadSchemaExporter
{
    public static JsonNode Export(JsonSerializerOptions options, Type type, string rootPointer)
    {
        if (options.ReferenceHandler is not null)
        {
            throw new NotSupportedException("AsyncAPI schema inference requires a schema override when a JSON reference handler is configured.");
        }

        return options.GetJsonSchemaAsNode(type, new JsonSchemaExporterOptions
        {
            TreatNullObliviousAsNonNullable = true,
            TransformSchemaNode = (context, node) =>
            {
                var converter = context.PropertyInfo?.CustomConverter ?? context.TypeInfo.Converter;

                if (context.TypeInfo.PolymorphismOptions is not null ||
                    converter.GetType().Assembly != typeof(JsonSerializer).Assembly ||
                    node.GetValueKind() == JsonValueKind.True)
                {
                    throw new NotSupportedException(
                        $"Cannot infer an AsyncAPI schema for '{context.TypeInfo.Type}' at '{string.Join('/', context.Path)}'. Supply an explicit schema.");
                }

                if (node is JsonObject schema && schema["$ref"] is JsonValue reference)
                {
                    var pointer = reference.GetValue<string>();
                    schema["$ref"] = rootPointer + pointer[1..];
                }

                return node;
            }
        });
    }

    public static void ValidateSchema(JsonNode schema)
    {
        ArgumentNullException.ThrowIfNull(schema);

        if (schema.GetValueKind() is not (JsonValueKind.Object or JsonValueKind.True or JsonValueKind.False))
        {
            throw new ArgumentException("An AsyncAPI schema must be a JSON object or boolean.", nameof(schema));
        }
    }
}
