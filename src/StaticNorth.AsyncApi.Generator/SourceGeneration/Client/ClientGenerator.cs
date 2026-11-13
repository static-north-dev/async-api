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
        var documents = context
            .AdditionalTextsProvider
            .Where(static file => file.Path.EndsWith(".asyncapi.json", StringComparison.OrdinalIgnoreCase))
            .Combine(context.AnalyzerConfigOptionsProvider)
            .Select(static (pair, cancellationToken) => Generate(pair.Left, pair.Right.GetOptions(pair.Left), cancellationToken));

        var compilationDocuments = documents
            .Collect()
            .Combine(context.CompilationProvider);

        context.RegisterSourceOutput(compilationDocuments, static (output, pair) =>
        {
            var ordered = pair.Left.OrderBy(document => document.Location.GetLineSpan().Path, StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var document in ordered)
            {
                if (document.Error is not null)
                {
                    output.ReportDiagnostic(document.Error);
                    continue;
                }

                var conflict = document.Symbols.FirstOrDefault(symbol =>
                    seen.Any(existing => NamesConflict(symbol, existing)) ||
                    HasNamespaceConflict(pair.Right.Assembly.GlobalNamespace, symbol));

                if (conflict is not null)
                {
                    output.ReportDiagnostic(Diagnostic.Create(SymbolConflict, document.Location,
                        $"Generated name '{conflict}' already exists. Use distinct AsyncApiNamespace or AsyncApiAlias metadata."));
                    continue;
                }

                seen.UnionWith(document.Symbols);

                if (document.Source.Length != 0)
                {
                    output.AddSource(document.Hint, document.Source);
                }
            }
        });
    }

    private static ClientOutput Generate(AdditionalText file, AnalyzerConfigOptions configuration, CancellationToken cancellationToken)
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
                var emitter = new ClientContractEmitter(root, options);
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

    private static bool NamesConflict(string symbol, string existing)
    {
        return symbol == existing || symbol.StartsWith(existing + ".", StringComparison.Ordinal) ||
            existing.StartsWith(symbol + ".", StringComparison.Ordinal);
    }

    private static bool HasNamespaceConflict(INamespaceSymbol root, string symbol)
    {
        var segments = symbol.Split('.');
        var current = root;

        for (var index = 0; index < segments.Length; index++)
        {
            var segment = segments[index];

            var types = current.GetTypeMembers(segment, 0);

            if (types.Any(type => !type.IsFileLocal))
            {
                return true;
            }

            var next = current
                .GetNamespaceMembers()
                .FirstOrDefault(member => member.Name == segment);

            if (next is null)
            {
                return false;
            }

            current = next;
        }

        return true;
    }

    private static DiagnosticDescriptor CreateDiagnostic(string id, string title)
    {
        return new DiagnosticDescriptor(id, title, "{0}", "AsyncAPI", DiagnosticSeverity.Error, isEnabledByDefault: true);
    }
}
