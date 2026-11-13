using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using StaticNorth.AsyncApi.Generator.SourceGeneration.Client;

namespace StaticNorth.AsyncApi.Generator.SourceGeneration;

[GeneratorAttribute(LanguageNames.CSharp)]
public sealed class ClientGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor InvalidDocument = CreateDiagnostic("SNA2001", "Invalid AsyncAPI document");
    private static readonly DiagnosticDescriptor UnsupportedDocument = CreateDiagnostic("SNA2002", "Unsupported AsyncAPI contract");
    private static readonly DiagnosticDescriptor SymbolConflict = CreateDiagnostic("SNA2003", "Conflicting AsyncAPI generated symbol");

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var clientAvailable = context
            .CompilationProvider
            .Select(static (compilation, _) =>
                compilation.GetTypeByMetadataName("StaticNorth.AsyncApi.Client.Transport.AsyncApiJsonClient") is not null);
        var documents = context
            .AdditionalTextsProvider
            .Where(static file => file.Path.EndsWith(".asyncapi.json", StringComparison.OrdinalIgnoreCase))
            .Combine(context.AnalyzerConfigOptionsProvider)
            .Combine(clientAvailable)
            .Select(static (pair, cancellationToken) =>
            {
                var (file, configuration) = pair.Left;
                return Generate(file, configuration.GetOptions(file), pair.Right, cancellationToken);
            });

        var checkedDocuments = documents
            .Combine(context.CompilationProvider)
            .Select(static (pair, cancellationToken) =>
                (Document: pair.Left, Conflict: FindCompilationConflict(
                    pair.Right.Assembly.GlobalNamespace, pair.Left.Symbols, cancellationToken)))
            .WithTrackingName("ClientCompilationConflicts")
            .Collect()
            .WithTrackingName("ClientDocuments");

        context.RegisterSourceOutput(checkedDocuments, static (output, documents) =>
        {
            var ordered = documents.OrderBy(item => item.Document.Location.GetLineSpan().Path, StringComparer.Ordinal);
            var seenTypes = new HashSet<string>(StringComparer.Ordinal);
            var seenNamespaces = new HashSet<string>(StringComparer.Ordinal);

            foreach (var (document, compilationConflict) in ordered)
            {
                output.CancellationToken.ThrowIfCancellationRequested();

                if (document.Error is not null)
                {
                    output.ReportDiagnostic(document.Error);
                    continue;
                }

                if (document.Symbols.Length == 0)
                {
                    continue;
                }

                var namespaceName = document.Symbols[0].Substring(0, document.Symbols[0].LastIndexOf('.'));
                var conflict = FindGeneratedConflict(document.Symbols, namespaceName, compilationConflict, seenTypes, seenNamespaces);

                if (conflict is not null)
                {
                    output.ReportDiagnostic(Diagnostic.Create(SymbolConflict, document.Location,
                        $"Generated name '{conflict}' already exists. Use distinct AsyncApiNamespace or AsyncApiAlias metadata."));
                    continue;
                }

                seenTypes.UnionWith(document.Symbols);
                foreach (var prefix in NamespacePrefixes(namespaceName))
                {
                    seenNamespaces.Add(prefix);
                }

                output.AddSource(document.Hint, document.Source);
            }
        });
    }

    private static ClientOutput Generate(
        AdditionalText file,
        AnalyzerConfigOptions configuration,
        bool clientAvailable,
        CancellationToken cancellationToken)
    {
        var location = Location.Create(file.Path, new TextSpan(0, 0), new LinePositionSpan(default, default));

        try
        {
            var text = file.GetText(cancellationToken) ?? throw new ContractException("#", "unable to read the additional file");
            var options = ClientOptions.Read(file.Path, configuration);
            using (var document = JsonDocument.Parse(text.ToString(), new JsonDocumentOptions { AllowDuplicateProperties = false }))
            {
                var root = new ContractNode(document.RootElement, "#");
                var version = root
                    .Field("asyncapi")
                    .String();

                if (version != "3.1.0")
                {
                    throw new ContractException("#/asyncapi", $"expected version 3.1.0, found '{version}'", unsupported: true);
                }

                var info = root.Field("info");
                info
                    .Field("title")
                    .String();
                info
                    .Field("version")
                    .String();
                var emitter = new ClientContractEmitter(root, options, clientAvailable);
                var (source, symbols) = emitter.Write(cancellationToken);
                var qualified = symbols
                    .Select(symbol => options.Namespace + "." + symbol)
                    .ToArray();
                var hint = qualified.Length == 0 ? "" : $"{qualified[0]}.AsyncApi.g.cs";
                return new ClientOutput(location, hint, source, qualified, null);
            }
        }
        catch (JsonException exception)
        {
            var diagnostic = Diagnostic.Create(InvalidDocument, location, exception.Message);
            return new ClientOutput(location, "", "", [], diagnostic);
        }
        catch (ContractException exception)
        {
            var descriptor = exception.Unsupported ? UnsupportedDocument : InvalidDocument;
            var diagnostic = Diagnostic.Create(descriptor, location, exception.Message);
            return new ClientOutput(location, "", "", [], diagnostic);
        }
    }

    private static string? FindGeneratedConflict(
        string[] symbols,
        string namespaceName,
        string? compilationConflict,
        HashSet<string> seenTypes,
        HashSet<string> seenNamespaces)
    {
        foreach (var prefix in NamespacePrefixes(namespaceName))
        {
            if (seenTypes.Contains(prefix))
            {
                return symbols[0];
            }
        }

        foreach (var symbol in symbols)
        {
            if (symbol == compilationConflict || seenTypes.Contains(symbol) || seenNamespaces.Contains(symbol))
            {
                return symbol;
            }
        }

        return null;
    }

    private static IEnumerable<string> NamespacePrefixes(string namespaceName)
    {
        for (var end = namespaceName.IndexOf('.'); end >= 0; end = namespaceName.IndexOf('.', end + 1))
        {
            yield return namespaceName.Substring(0, end);
        }

        yield return namespaceName;
    }

    private static string? FindCompilationConflict(INamespaceSymbol current, string[] symbols, CancellationToken cancellationToken)
    {
        if (symbols.Length == 0)
        {
            return null;
        }

        var first = symbols[0];
        var typeStart = first.LastIndexOf('.') + 1;
        var offset = 0;

        while (offset < typeStart)
        {
            var end = first.IndexOf('.', offset);
            var segment = first.Substring(offset, end - offset);
            INamespaceSymbol? next = null;

            foreach (var member in current.GetMembers(segment))
            {
                if (member is INamedTypeSymbol { Arity: 0, IsFileLocal: false })
                {
                    return first;
                }

                if (member is INamespaceSymbol child)
                {
                    next = child;
                }
            }

            if (next is null)
            {
                return null;
            }

            current = next;
            offset = end + 1;
        }

        foreach (var symbol in symbols)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var member in current.GetMembers(symbol.Substring(typeStart)))
            {
                if (member is INamespaceSymbol or INamedTypeSymbol { Arity: 0, IsFileLocal: false })
                {
                    return symbol;
                }
            }
        }

        return null;
    }

    private static DiagnosticDescriptor CreateDiagnostic(string id, string title)
    {
        return new DiagnosticDescriptor(id, title, "{0}", "AsyncAPI", DiagnosticSeverity.Error, isEnabledByDefault: true);
    }
}
