using System.Globalization;
using System.Text.Json;

namespace StaticNorth.AsyncApi.Generator.SourceGeneration.Client;

internal sealed class ContractReferences(ContractNode root)
{
    internal ContractNode ResolveObject(ContractNode node)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);

        while (true)
        {
            var reference = node.Field("$ref");

            if (!reference.Exists)
            {
                return node;
            }

            if (!seen.Add(node.Path))
            {
                throw new ContractException(reference.Path, "cyclic reference chain");
            }

            node = Resolve(reference);
        }
    }

    internal ContractNode Resolve(ContractNode reference)
    {
        var node = root;

        foreach (var segment in ReadPointer(reference))
        {
            if (node.Kind == JsonValueKind.Object)
            {
                node = node.Field(segment);
            }
            else if (node.Kind == JsonValueKind.Array &&
                     int.TryParse(segment, NumberStyles.None, CultureInfo.InvariantCulture, out var index) &&
                     segment == index.ToString(CultureInfo.InvariantCulture) && index < node.Value.GetArrayLength())
            {
                node = new ContractNode(node.Value[index], node.Path + "/" + segment);
            }
            else
            {
                throw new ContractException(reference.Path, $"unresolved local reference '{reference.String()}'");
            }

            if (!node.Exists)
            {
                throw new ContractException(reference.Path, $"unresolved local reference '{reference.String()}'");
            }
        }

        return node;
    }

    internal static string[] ReadPointer(ContractNode reference)
    {
        var pointer = reference.String();

        if (!pointer.StartsWith("#", StringComparison.Ordinal))
        {
            throw new ContractException(reference.Path, "external references require a bundled document", unsupported: true);
        }

        for (var index = 1; index < pointer.Length; index++)
        {
            if (pointer[index] == '%' && (index + 2 >= pointer.Length ||
                !Uri.IsHexDigit(pointer[index + 1]) || !Uri.IsHexDigit(pointer[index + 2])))
            {
                throw new ContractException(reference.Path, $"invalid URI fragment '{pointer}'");
            }
        }

        var fragment = Uri.UnescapeDataString(pointer.Substring(1));

        if (fragment.Length == 0)
        {
            return [];
        }

        if (fragment[0] != '/')
        {
            throw new ContractException(reference.Path, $"invalid JSON Pointer '{pointer}'");
        }

        var segments = fragment
            .Substring(1)
            .Split('/');

        for (var index = 0; index < segments.Length; index++)
        {
            var segment = segments[index];

            for (var position = 0; position < segment.Length; position++)
            {
                if (segment[position] == '~' && (++position == segment.Length || segment[position] is not ('0' or '1')))
                {
                    throw new ContractException(reference.Path, $"invalid JSON Pointer escape in '{pointer}'");
                }
            }

            segments[index] = segment
                .Replace("~1", "/")
                .Replace("~0", "~");
        }

        return segments;
    }
}
