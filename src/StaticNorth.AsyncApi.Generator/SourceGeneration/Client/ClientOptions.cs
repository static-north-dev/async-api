using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace StaticNorth.AsyncApi.Generator.SourceGeneration.Client;

internal sealed class ClientOptions(string @namespace, string modelPrefix, string mode)
{
    internal string Namespace { get; } = @namespace;
    internal string ModelPrefix { get; } = modelPrefix;
    internal bool Publishers => mode is "both" or "publishers";
    internal bool Subscribers => mode is "both" or "subscribers";

    internal static ClientOptions Read(string path, AnalyzerConfigOptions options)
    {
        var filename = Path.GetFileName(path);
        var alias = ReadMetadata(options, "AsyncApiAlias") ?? filename.Substring(0, filename.Length - ".asyncapi.json".Length);
        var @namespace = ReadMetadata(options, "AsyncApiNamespace") ?? $"AsyncApi.Generated.{ClientNames.Identifier(alias)}";

        var namespaceParts = @namespace.Split('.');

        if (namespaceParts.Any(part => !SyntaxFacts.IsValidIdentifier(part) ||
            SyntaxFacts.GetKeywordKind(part) != SyntaxKind.None || part.StartsWith("@", StringComparison.Ordinal)))
        {
            throw new ContractException("AsyncApiNamespace", $"'{@namespace}' is not a valid C# namespace");
        }

        var prefix = ReadMetadata(options, "AsyncApiModelPrefix");
        var mode = ReadMetadata(options, "AsyncApiGenerate")?.ToLowerInvariant() ?? "both";

        if (mode is not ("both" or "publishers" or "subscribers"))
        {
            throw new ContractException("AsyncApiGenerate", "use 'both', 'publishers', or 'subscribers'");
        }

        return new ClientOptions(@namespace, prefix is null ? "" : ClientNames.Identifier(prefix), mode);
    }

    private static string? ReadMetadata(AnalyzerConfigOptions options, string name)
    {
        return options.TryGetValue($"build_metadata.AdditionalFiles.{name}", out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;
    }
}
