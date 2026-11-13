using Microsoft.CodeAnalysis;
using ReflectionAssembly = System.Reflection.Assembly;

namespace StaticNorth.AsyncApi.Tests;

internal static class GeneratorTestAssembly
{
    private static readonly ReflectionAssembly Analyzer = ReflectionAssembly.LoadFrom(
        Path.Combine(AppContext.BaseDirectory, "StaticNorth.AsyncApi.Generator.Analyzers.dll"));

    internal static IIncrementalGenerator Create(string name)
    {
        var type = Analyzer.GetType($"StaticNorth.AsyncApi.Generator.SourceGeneration.{name}", throwOnError: true)!;
        return (IIncrementalGenerator)Activator.CreateInstance(type)!;
    }
}
