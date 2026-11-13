using System.Text;
using Microsoft.CodeAnalysis.CSharp;

namespace StaticNorth.AsyncApi.Generator.SourceGeneration.Client;

internal static class ClientNames
{
    internal static string Identifier(string value)
    {
        var result = new StringBuilder();
        var capitalize = true;

        foreach (var character in value)
        {
            if (!char.IsLetterOrDigit(character))
            {
                capitalize = true;
                continue;
            }

            if (result.Length == 0 && !char.IsLetter(character))
            {
                result.Append('_');
            }

            result.Append(capitalize ? char.ToUpperInvariant(character) : character);
            capitalize = false;
        }

        return result.Length == 0 ? "Value" : result.ToString();
    }

    internal static string Literal(string value) => SymbolDisplay.FormatLiteral(value, quote: true);
}
