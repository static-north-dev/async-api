using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;

namespace StaticNorth.AsyncApi.Generator.SourceGeneration;

/// <summary>Generates registration code for annotated AsyncAPI definition classes.</summary>
[GeneratorAttribute(LanguageNames.CSharp)]
public sealed class ProducerGenerator : IIncrementalGenerator
{
    private const string RuntimeNamespace = "StaticNorth.AsyncApi.Generator";
    private const string RegistrationName = "StaticNorth.AsyncApi.Generated.AsyncApiDefinitions";

    private static readonly SymbolDisplayFormat QualifiedType = SymbolDisplayFormat.FullyQualifiedFormat
        .AddMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    private static readonly DiagnosticDescriptor InvalidDefinition = new(
        "SNA1001",
        "Invalid AsyncAPI definition",
        "AsyncAPI definition '{0}' {1}",
        "AsyncAPI",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor RegistrationConflict = new(
        "SNA1002",
        "AsyncAPI registration type already exists",
        "Rename '{0}' to allow AsyncAPI to generate its registration type",
        "AsyncAPI",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidates = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                RuntimeNamespace + ".AsyncApiDefinitionAttribute",
                static (_, _) => true,
                static (attribute, _) => GetRegistration(attribute));
        var registrations = candidates
            .Where(static candidate => candidate.Error is null)
            .Select(static (candidate, _) => (candidate.Name, candidate.Registration))
            .Collect();
        var conflict = context.CompilationProvider
            .Select(static (compilation, _) => FindConflict(compilation.Assembly.GlobalNamespace));

        context.RegisterSourceOutput(candidates, static (output, candidate) =>
        {
            if (candidate.Error is not null)
            {
                output.ReportDiagnostic(Diagnostic.Create(InvalidDefinition, candidate.Location, candidate.Name, candidate.Error));
            }
        });
        context.RegisterSourceOutput(registrations.Combine(conflict), static (output, pair) =>
        {
            if (pair.Left.IsEmpty)
            {
                return;
            }

            if (pair.Right is not null)
            {
                output.ReportDiagnostic(Diagnostic.Create(RegistrationConflict, pair.Right, RegistrationName));
                return;
            }

            output.AddSource("AsyncApiDefinitions.g.cs", WriteRegistration(pair.Left));
        });
    }

    private static (string Name, string Registration, Location Location, string? Error) GetRegistration(
        GeneratorAttributeSyntaxContext context)
    {
        var type = (INamedTypeSymbol)context.TargetSymbol;
        var name = type.ToDisplayString(QualifiedType);
        var location = context.TargetNode.GetLocation();
        var compilation = context.SemanticModel.Compilation;

        if (type.IsAbstract || !CanReference(type, compilation, allowGenerics: false))
        {
            return (name, "", location, "must be a concrete, accessible, non-generic class.");
        }

        var constructor = type.InstanceConstructors.FirstOrDefault(constructor =>
            constructor.Parameters.IsEmpty && compilation.IsSymbolAccessibleWithin(constructor, compilation.Assembly));

        if (constructor is null || RequiresInitializer(type, constructor))
        {
            return (name, "", location, "must have an accessible parameterless constructor that satisfies required members.");
        }

        var message = compilation.GetTypeByMetadataName(RuntimeNamespace + ".IAsyncApiMessageDefinition`1");
        var channel = compilation.GetTypeByMetadataName(RuntimeNamespace + ".IAsyncApiChannelDefinition");
        var operation = compilation.GetTypeByMetadataName(RuntimeNamespace + ".IAsyncApiOperationDefinition");
        var contracts = type.AllInterfaces.Where(contract =>
            SymbolEqualityComparer.Default.Equals(contract.OriginalDefinition, message) ||
            SymbolEqualityComparer.Default.Equals(contract, channel) ||
            SymbolEqualityComparer.Default.Equals(contract, operation));
        var supported = contracts.ToArray();

        if (supported.Length != 1)
        {
            return (name, "", location, "must implement exactly one AsyncAPI message, channel, or operation definition interface.");
        }

        var definition = supported[0];
        string method;

        if (definition.IsGenericType)
        {
            var payload = definition.TypeArguments[0];

            if (!CanReference(payload, compilation, allowGenerics: true))
            {
                return (name, "", location, "uses a message payload type that the generated registration cannot access.");
            }

            method = $"AddMessage<{payload.ToDisplayString(QualifiedType)}>";
        }
        else
        {
            method = SymbolEqualityComparer.Default.Equals(definition, channel) ? "AddChannel" : "AddOperation";
        }

        return (name, $"builder.{method}(static () => new {name}());", location, null);
    }

    private static bool CanReference(ITypeSymbol type, Compilation compilation, bool allowGenerics)
    {
        if (type is IArrayTypeSymbol array)
        {
            return CanReference(array.ElementType, compilation, allowGenerics);
        }

        if (type.TypeKind == TypeKind.Error || !compilation.IsSymbolAccessibleWithin(type, compilation.Assembly))
        {
            return false;
        }

        for (var current = type as INamedTypeSymbol; current is not null; current = current.ContainingType)
        {
            if (current.IsFileLocal || (current.IsGenericType && !allowGenerics) ||
                current.TypeArguments.Any(argument => !CanReference(argument, compilation, allowGenerics)))
            {
                return false;
            }
        }

        return true;
    }

    private static bool RequiresInitializer(INamedTypeSymbol type, IMethodSymbol constructor)
    {
        var hasRequiredMembers = false;

        for (var current = type; current is not null && !hasRequiredMembers; current = current.BaseType)
        {
            hasRequiredMembers = current.GetMembers().Any(member =>
                member is IPropertySymbol { IsRequired: true } or IFieldSymbol { IsRequired: true });
        }

        return hasRequiredMembers && !constructor.GetAttributes().Any(attribute =>
            attribute.AttributeClass?.ToDisplayString() == "System.Diagnostics.CodeAnalysis.SetsRequiredMembersAttribute");
    }

    private static Location? FindConflict(INamespaceSymbol root)
    {
        var current = root;

        foreach (var name in RegistrationName.Split('.'))
        {
            var type = current
                .GetTypeMembers(name, 0)
                .FirstOrDefault(member => !member.IsFileLocal);

            if (type is not null)
            {
                return type.Locations.FirstOrDefault();
            }

            var next = current
                .GetNamespaceMembers()
                .FirstOrDefault(member => member.Name == name);

            if (next is null)
            {
                return null;
            }

            current = next;
        }

        return current.Locations.FirstOrDefault();
    }

    private static string WriteRegistration(ImmutableArray<(string Name, string Registration)> definitions)
    {
        var source = new StringBuilder("""
            // <auto-generated/>
            #nullable enable

            namespace StaticNorth.AsyncApi.Generated;

            internal static class AsyncApiDefinitions
            {
                public static void Register(global::StaticNorth.AsyncApi.Generator.AsyncApiDocumentBuilder builder)
                {
            """);
        source.Append('\n');

        var registrations = definitions
            .Distinct()
            .OrderBy(static definition => definition.Name, StringComparer.Ordinal);

        foreach (var definition in registrations)
        {
            source
                .Append("        ")
                .Append(definition.Registration)
                .Append('\n');
        }

        source.Append("    }\n}\n");
        return source.ToString();
    }
}
