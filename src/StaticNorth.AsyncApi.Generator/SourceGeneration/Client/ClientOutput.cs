using Microsoft.CodeAnalysis;

namespace StaticNorth.AsyncApi.Generator.SourceGeneration.Client;

internal sealed class ClientOutput(Location location, string hint, string source, string[] symbols, Diagnostic? error)
{
    internal Location Location { get; } = location;
    internal string Hint { get; } = hint;
    internal string Source { get; } = source;
    internal string[] Symbols { get; } = symbols;
    internal Diagnostic? Error { get; } = error;
}
