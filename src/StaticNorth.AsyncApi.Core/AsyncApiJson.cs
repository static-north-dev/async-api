using System.Buffers;
using System.Text.Json;

namespace StaticNorth.AsyncApi.Core;

public static class AsyncApiJson
{
    public const string SupportedVersion = "3.1.0";

    public static AsyncApiDocument Read(ReadOnlyMemory<byte> utf8Json)
    {
        var options = new JsonDocumentOptions { AllowDuplicateProperties = false };
        using (var document = JsonDocument.Parse(utf8Json, options))
        {
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException("The AsyncAPI document root must be an object.");
            }

            var version = ReadRequiredString(root, "asyncapi", "asyncapi");

            if (version != SupportedVersion)
            {
                throw new JsonException($"AsyncAPI version '{version}' is unsupported. Expected '{SupportedVersion}'.");
            }

            if (!root.TryGetProperty("info", out var info) || info.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException("The required AsyncAPI 'info' field must be an object.");
            }

            ValidateOptionalObject(root, "servers");
            ValidateOptionalObject(root, "channels");
            ValidateOptionalObject(root, "operations");
            ValidateOptionalObject(root, "components");

            var metadata = new AsyncApiInfo(
                ReadRequiredString(info, "title", "info.title"),
                ReadRequiredString(info, "version", "info.version"));

            return new AsyncApiDocument(version, metadata, root.Clone());
        }
    }

    public static byte[] Write(AsyncApiDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            WriteElement(writer, document.Root);
        }

        return buffer.WrittenSpan.ToArray();
    }

    private static string ReadRequiredString(JsonElement parent, string name, string path)
    {
        if (!parent.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
        {
            throw new JsonException($"The required AsyncAPI '{path}' field must be a string.");
        }

        return value.GetString()!;
    }

    private static void ValidateOptionalObject(JsonElement root, string name)
    {
        if (root.TryGetProperty(name, out var section) && section.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException($"The AsyncAPI '{name}' field must be an object.");
        }
    }

    private static void WriteElement(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                {
                    writer.WriteStartObject();
                    var properties = element
                        .EnumerateObject()
                        .OrderBy(property => property.Name, StringComparer.Ordinal);

                    foreach (var property in properties)
                    {
                        writer.WritePropertyName(property.Name);
                        WriteElement(writer, property.Value);
                    }

                    writer.WriteEndObject();
                    break;
                }
            case JsonValueKind.Array:
                {
                    writer.WriteStartArray();

                    foreach (var item in element.EnumerateArray())
                    {
                        WriteElement(writer, item);
                    }

                    writer.WriteEndArray();
                    break;
                }
            default:
                {
                    element.WriteTo(writer);
                    break;
                }
        }
    }
}
