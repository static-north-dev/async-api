using System.Text;
using System.Xml;
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

    internal static string XmlText(string value)
    {
        var text = new StringBuilder(value.Length);

        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];

            if (char.IsHighSurrogate(character) && index + 1 < value.Length && char.IsLowSurrogate(value[index + 1]))
            {
                text.Append(value, index, 2);
                index++;
                continue;
            }

            switch (character)
            {
                case '&':
                    text.Append("&amp;");
                    break;
                case '<':
                    text.Append("&lt;");
                    break;
                case '>':
                    text.Append("&gt;");
                    break;
                case '\r' or '\n' or '\u0085' or '\u2028' or '\u2029':
                    text.Append(' ');
                    break;
                default:
                    text.Append(XmlConvert.IsXmlChar(character) ? character : '\uFFFD');
                    break;
            }
        }

        return text.ToString();
    }
}
