using Microsoft.CodeAnalysis;

namespace StaticNorth.AsyncApi.Generator.SourceGeneration;

[GeneratorAttribute(LanguageNames.CSharp)]
public sealed class ProducerGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
    }
}
