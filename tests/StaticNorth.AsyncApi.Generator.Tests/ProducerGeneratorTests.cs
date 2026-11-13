using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using StaticNorth.AsyncApi.Generator.SourceGeneration;

namespace StaticNorth.AsyncApi.Generator.Tests;

public sealed class ProducerGeneratorTests
{
    [Test]
    public async Task GeneratesCompilableRegistrationInTypeNameOrder()
    {
        const string declarations = """
            using StaticNorth.AsyncApi.Generator;

            [AsyncApiDefinition]
            internal sealed class Zebra : IAsyncApiChannelDefinition
            {
                public void Configure(IChannelBuilder channel) => throw new System.Exception();
            }

            internal static class Container
            {
                [AsyncApiDefinition]
                public sealed class @event : IAsyncApiMessageDefinition<string>
                {
                    public void Configure(IMessageBuilder<string> message) => throw new System.Exception();
                }
            }
            """;
        var compilation = CreateCompilation(declarations);
        var generator = new ProducerGenerator();
        var driver = CSharpGeneratorDriver.Create(generator.AsSourceGenerator());
        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);
        var result = driver.GetRunResult();
        var source = result.GeneratedTrees
            .Single()
            .ToString();
        var message = source.IndexOf("new global::Container.@event()", StringComparison.Ordinal);
        var channel = source.IndexOf("new global::Zebra()", StringComparison.Ordinal);

        await Assert
            .That(output.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
            .IsEmpty();
        await Assert
            .That(message >= 0 && channel > message)
            .IsTrue();
    }

    [Test]
    [Arguments("[AsyncApiDefinition] class Invalid;", "SNA1001")]
    [Arguments("[AsyncApiDefinition] class Invalid<T> : Channel;", "SNA1001")]
    [Arguments("class Outer<T> { [AsyncApiDefinition] internal class Invalid : Channel; }", "SNA1001")]
    [Arguments("[AsyncApiDefinition] file class Invalid : Channel;", "SNA1001")]
    [Arguments("[AsyncApiDefinition] class Invalid : Channel { private Invalid() {} }", "SNA1001")]
    [Arguments("[AsyncApiDefinition] class Invalid : Channel, IAsyncApiOperationDefinition { public void Configure(IOperationBuilder operation) {} }", "SNA1001")]
    [Arguments("namespace StaticNorth.AsyncApi.Generated { class AsyncApiDefinitions; } [AsyncApiDefinition] class Valid : Channel;", "SNA1002")]
    public async Task ReportsInvalidDeclarationsAtTheirSourceLocation(string declaration, string expectedId)
    {
        var compilation = CreateCompilation($$"""
            using StaticNorth.AsyncApi.Generator;
            class Channel : IAsyncApiChannelDefinition
            {
                public void Configure(IChannelBuilder channel) { }
            }
            {{declaration}}
            """);
        var generator = new ProducerGenerator();
        var driver = CSharpGeneratorDriver.Create(generator.AsSourceGenerator());
        var result = driver
            .RunGenerators(compilation)
            .GetRunResult();
        var diagnostic = result.Diagnostics.Single();

        await Assert
            .That(diagnostic.Id)
            .IsEqualTo(expectedId);
        await Assert
            .That(diagnostic.Location.IsInSource)
            .IsTrue();
        await Assert
            .That(result.GeneratedTrees)
            .IsEmpty();
    }

    [Test]
    public async Task RunsAgainstAnUnconfiguredCompilation()
    {
        var compilation = CSharpCompilation.Create("Producer");
        var generator = new ProducerGenerator();
        var driver = CSharpGeneratorDriver.Create(generator.AsSourceGenerator());
        var result = driver
            .RunGenerators(compilation)
            .GetRunResult();

        await Assert
            .That(result.Diagnostics)
            .IsEmpty();
        await Assert
            .That(result.GeneratedTrees)
            .IsEmpty();
    }

    private static CSharpCompilation CreateCompilation(string source)
    {
        var platformAssemblies = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!;
        var references = platformAssemblies
            .Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path));

        return CSharpCompilation.Create(
            "Producer",
            [CSharpSyntaxTree.ParseText(source)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }
}
