using System.Text.RegularExpressions;

namespace LegacyLens.Core.Rules.Parsing;

internal static class Calls
{
    /// <summary>
    /// Removes every call matched by <paramref name="callStart"/> together with its arguments.
    /// <paramref name="callStart"/> must match the function name up to and including the "(".
    /// Example: <c>Server.HTMLEncode(a) &amp; b</c> becomes <c> &amp; b</c>.
    /// </summary>
    public static string Remove(string text, Regex callStart)
    {
        var match = callStart.Match(text);
        while (match.Success)
        {
            var open = match.Index + match.Length - 1;
            var close = ClosingParenthesis(text, open);
            text = text.Remove(match.Index, close - match.Index + 1);
            match = callStart.Match(text, match.Index);
        }

        return text;
    }

    private static int ClosingParenthesis(string text, int open)
    {
        var depth = 0;
        for (var i = open; i < text.Length; i++)
        {
            if (text[i] == '(')
            {
                depth++;
            }
            else if (text[i] == ')' && --depth == 0)
            {
                return i;
            }
        }

        return text.Length - 1;
    }
}
