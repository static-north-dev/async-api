using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace StaticNorth.AsyncApi.Client.Tests;

internal sealed class NamespaceOptionsProvider(string parentPath, string childPath) : AnalyzerConfigOptionsProvider
{
    public override AnalyzerConfigOptions GlobalOptions { get; } = new NamespaceOptions(null);

    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree)
    {
        return GlobalOptions;
    }

    public override AnalyzerConfigOptions GetOptions(AdditionalText text)
    {
        var name = text.Path == parentPath ? "Contracts" : text.Path == childPath ? "Contracts.SendMetadata" : null;
        return new NamespaceOptions(name);
    }

    private sealed class NamespaceOptions(string? name) : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, out string value)
        {
            value = name ?? "";
            return name is not null && key == "build_metadata.AdditionalFiles.AsyncApiNamespace";
        }
    }
}
