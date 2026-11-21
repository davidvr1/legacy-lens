using System.Text;
using LegacyLens.Core.Model;

namespace LegacyLens.Core.Rules.Parsing;

/// <summary>
/// Keeps only server-side code: removes comments so rules do not flag commented-out code, and
/// replaces text outside <c>&lt;% %&gt;</c> blocks (HTML) with spaces. Line numbers are preserved.
/// Comment syntax: VBScript <c>'</c> and <c>Rem</c> for Classic ASP, <c>//</c> for WebForms (C#).
/// </summary>
internal static class CodeScanner
{
    public static IReadOnlyList<string> StripComments(IReadOnlyList<string> lines, SourceLanguage language)
    {
        // A file without <% %> blocks (code-behind, a pure VBScript include) is all code.
        var inCode = !lines.Any(line => line.Contains("<%", StringComparison.Ordinal));

        var result = new string[lines.Count];
        for (var i = 0; i < lines.Count; i++)
        {
            result[i] = StripLine(lines[i], language, ref inCode);
        }

        return result;
    }

    private static string StripLine(string line, SourceLanguage language, ref bool inCode)
    {
        var output = new StringBuilder(line.Length);
        var inString = false;
        var inComment = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];

            if (!inCode)
            {
                if (c == '<' && At(line, i + 1) == '%')
                {
                    inCode = true;
                    output.Append(c);
                }
                else
                {
                    // HTML is blanked so quotes or apostrophes in it cannot confuse code patterns.
                    output.Append(' ');
                }

                continue;
            }

            // "%>" ends the code block, even inside a VBScript comment.
            if (!inString && c == '%' && At(line, i + 1) == '>')
            {
                inCode = false;
                inComment = false;
                output.Append("%>");
                i++;
                continue;
            }

            if (inComment)
            {
                continue;
            }

            if (inString)
            {
                output.Append(c);
                if (c == '\\' && language == SourceLanguage.WebForms && i + 1 < line.Length)
                {
                    output.Append(line[++i]);
                }
                else if (c == '"')
                {
                    inString = false;
                }

                continue;
            }

            if (c == '"')
            {
                inString = true;
                output.Append(c);
                continue;
            }

            if (language == SourceLanguage.WebForms && c == '\'')
            {
                // C# char literal such as '"' or '\''.
                var length = At(line, i + 1) == '\\' ? 4 : 3;
                var end = Math.Min(i + length, line.Length);
                output.Append(line, i, end - i);
                i = end - 1;
                continue;
            }

            if (IsCommentStart(line, i, language, output))
            {
                inComment = true;
                continue;
            }

            output.Append(c);
        }

        return output.ToString();
    }

    private static bool IsCommentStart(string line, int index, SourceLanguage language, StringBuilder output)
    {
        if (language == SourceLanguage.WebForms)
        {
            return line[index] == '/' && At(line, index + 1) == '/';
        }

        if (line[index] == '\'')
        {
            return true;
        }

        // VBScript "Rem" comment at the start of a statement.
        return IsWord(line, index, "rem") && IsStatementStart(output);
    }

    private static bool IsWord(string line, int index, string word)
    {
        if (string.Compare(line, index, word, 0, word.Length, StringComparison.OrdinalIgnoreCase) != 0)
        {
            return false;
        }

        var before = index == 0 ? ' ' : line[index - 1];
        var after = At(line, index + word.Length);
        return !IsIdentifierChar(before) && !IsIdentifierChar(after);
    }

    private static bool IsStatementStart(StringBuilder output)
    {
        var text = output.ToString().TrimEnd();
        return text.Length == 0
            || text.EndsWith("<%", StringComparison.Ordinal)
            || text.EndsWith(':');
    }

    private static bool IsIdentifierChar(char c) => char.IsLetterOrDigit(c) || c == '_';

    private static char At(string line, int index) => index < line.Length ? line[index] : '\0';
}
