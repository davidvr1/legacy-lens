using System.Text;
using LegacyLens.Core.Model;

namespace LegacyLens.Core.Rules.Parsing;

/// <param name="Masked">
/// The line with the contents of every string literal replaced by spaces. It has the same length
/// as the original, so match positions can be used to read the original text.
/// </param>
/// <param name="Literals">The contents of the string literals, in order.</param>
internal sealed record LiteralSplit(string Masked, IReadOnlyList<string> Literals);

internal static class StringLiterals
{
    /// <summary>
    /// Separates string literals from code, so patterns that look for code do not match text
    /// inside strings. VBScript escapes a quote as <c>""</c>; C# also uses backslash escapes.
    /// </summary>
    public static LiteralSplit Split(string line, SourceLanguage language)
    {
        var masked = new StringBuilder(line.Length);
        var literals = new List<string>();
        var current = new StringBuilder();
        var inString = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];

            if (!inString)
            {
                if (language == SourceLanguage.WebForms && c == '\'')
                {
                    // C# char literal such as '"' or '\''.
                    var length = i + 1 < line.Length && line[i + 1] == '\\' ? 4 : 3;
                    var end = Math.Min(i + length, line.Length);
                    masked.Append(line, i, end - i);
                    i = end - 1;
                    continue;
                }

                masked.Append(c);
                if (c == '"')
                {
                    inString = true;
                    current.Clear();
                }

                continue;
            }

            if (c == '\\' && language == SourceLanguage.WebForms && i + 1 < line.Length)
            {
                current.Append(c).Append(line[i + 1]);
                masked.Append("  ");
                i++;
                continue;
            }

            if (c == '"')
            {
                inString = false;
                literals.Add(current.ToString());
                masked.Append(c);
                continue;
            }

            current.Append(c);
            masked.Append(' ');
        }

        if (inString)
        {
            literals.Add(current.ToString());
        }

        return new LiteralSplit(masked.ToString(), literals);
    }
}
