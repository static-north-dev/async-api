using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace StaticNorth.AsyncApi.Client.Tests;

internal sealed class ContractFile(string path, string content) : AdditionalText
{
    public override string Path => path;

    public override SourceText GetText(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return SourceText.From(content);
    }
}
