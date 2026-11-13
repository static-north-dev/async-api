using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using StaticNorth.AsyncApi.Generator.SourceGeneration;

namespace StaticNorth.AsyncApi.Generator.Tests;

public sealed class ProducerGeneratorTests
{
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
}
