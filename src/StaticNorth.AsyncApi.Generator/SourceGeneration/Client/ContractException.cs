namespace StaticNorth.AsyncApi.Generator.SourceGeneration.Client;

internal sealed class ContractException(string path, string message, bool unsupported = false)
    : Exception($"{path}: {message}")
{
    internal bool Unsupported { get; } = unsupported;
}
