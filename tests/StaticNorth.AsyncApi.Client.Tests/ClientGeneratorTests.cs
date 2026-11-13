using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using StaticNorth.AsyncApi.Generator.SourceGeneration;

namespace StaticNorth.AsyncApi.Client.Tests;

public sealed class ClientGeneratorTests
{
    [Test]
    public async Task RunsAgainstACompilationWithoutAdditionalFiles()
    {
        var compilation = CSharpCompilation.Create("Consumer");
        var generator = new ClientGenerator();
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
}
