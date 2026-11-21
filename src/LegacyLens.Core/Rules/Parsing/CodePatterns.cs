using System.Text.RegularExpressions;

namespace LegacyLens.Core.Rules.Parsing;

/// <summary>Patterns shared by several rules. Use them on lines whose string literals are masked.</summary>
internal static partial class CodePatterns
{
    /// <summary>Reads request data: Request("x"), Request["x"], Request.QueryString, Request.Form, ...</summary>
    [GeneratedRegex(
        @"\bRequest\s*(?:\(|\[|\.\s*(?:QueryString|Form|Cookies|Params|ServerVariables|Item)\b)",
        RegexOptions.IgnoreCase)]
    public static partial Regex RequestData();

    /// <summary>Writes to the page: Response.Write or &lt;%= %&gt;.</summary>
    [GeneratedRegex(@"\bResponse\s*\.\s*Write\b|<%=", RegexOptions.IgnoreCase)]
    public static partial Regex Output();
}
