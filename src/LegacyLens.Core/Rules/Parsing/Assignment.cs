using System.Text.RegularExpressions;

namespace LegacyLens.Core.Rules.Parsing;

/// <summary>A simple assignment such as <c>sql = "..." &amp; id</c> or <c>string sql = "..." + id;</c>.</summary>
internal sealed partial record Assignment(string Target, string Value)
{
    /// <summary>Parses an assignment from a line whose string literals are masked.</summary>
    public static Assignment? TryParse(string maskedLine)
    {
        var match = Pattern().Match(maskedLine);
        return match.Success
            ? new Assignment(match.Groups["target"].Value, match.Groups["value"].Value)
            : null;
    }

    // Optional "<%", optional declaration keyword, a target (variable, member, Session("x"), x["y"]),
    // then =, += or &= but not ==.
    [GeneratedRegex(
        """^\s*(?:<%\s*)?(?:(?:Dim|Set|Const|var|string)\s+)?(?<target>[A-Za-z_][\w.]*(?:\([^()]*\)|\[[^\[\]]*\])?)\s*(?:\+=|&=|=)(?!=)(?<value>.*)$""",
        RegexOptions.IgnoreCase)]
    private static partial Regex Pattern();
}
