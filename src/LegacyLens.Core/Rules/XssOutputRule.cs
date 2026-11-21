using System.Text.RegularExpressions;
using LegacyLens.Core.Model;
using LegacyLens.Core.Rules.Parsing;

namespace LegacyLens.Core.Rules;

/// <summary>
/// XSS001: request data written to the page with Response.Write or &lt;%= %&gt; without HTML encoding.
/// Tracks variables assigned from request data (<c>name = Request("name")</c>) through the file.
/// </summary>
public sealed partial class XssOutputRule : IRule
{
    public string Id => "XSS001";

    public IReadOnlyList<Finding> Analyze(SourceFile file)
    {
        var findings = new List<Finding>();
        var tainted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < file.CodeLines.Count; i++)
        {
            var masked = StringLiterals.Split(file.CodeLines[i], file.Language).Masked;

            foreach (Match output in OutputExpression().Matches(masked))
            {
                var source = UnencodedInput(output.Groups["expr"].Value, tainted);
                if (source is not null)
                {
                    findings.Add(CreateFinding(i + 1, source));
                    break;
                }
            }

            var assignment = Assignment.TryParse(masked);
            if (assignment is not null)
            {
                if (IsTainted(assignment.Value, tainted))
                {
                    tainted.Add(assignment.Target);
                }
                else
                {
                    tainted.Remove(assignment.Target);
                }
            }
        }

        return findings;
    }

    // Describes the unencoded request data in an output expression, or returns null if there is none.
    private static string? UnencodedInput(string expression, IReadOnlySet<string> tainted)
    {
        var unencoded = Calls.Remove(expression, SafeCall());
        if (CodePatterns.RequestData().IsMatch(unencoded))
        {
            return "Request data";
        }

        var variable = tainted.FirstOrDefault(name => References(unencoded, name));
        return variable is null ? null : $"The variable '{variable}', which holds request data,";
    }

    private static bool IsTainted(string value, IReadOnlySet<string> tainted)
    {
        var unencoded = Calls.Remove(value, SafeCall());
        if (CodePatterns.RequestData().IsMatch(unencoded))
        {
            return true;
        }

        // Only copies and concatenations pass taint on; a call result such as conn.Execute(sql) does not.
        var withoutCalls = Calls.Remove(unencoded, AnyCall());
        return tainted.Any(name => References(withoutCalls, name));
    }

    private static bool References(string text, string variable) =>
        Regex.IsMatch(text, $@"(?<![\w.]){Regex.Escape(variable)}(?!\w)", RegexOptions.IgnoreCase);

    private Finding CreateFinding(int line, string source) => new(
        Id,
        Severity.High,
        line,
        "Request data written to the page without encoding",
        $"{source} is written to the page without HTML encoding. An attacker can inject script into the page (cross-site scripting).",
        "Encode the value with Server.HTMLEncode (Classic ASP) or HttpUtility.HtmlEncode. In ASP.NET Core Razor, @value is encoded by default.");

    [GeneratedRegex(@"(?:\bResponse\s*\.\s*Write\b|<%=)(?<expr>.*?)(?=%>|$)", RegexOptions.IgnoreCase)]
    private static partial Regex OutputExpression();

    // Calls whose result is safe to write to HTML: encoders and numeric conversions.
    [GeneratedRegex(
        @"(?<![\w.])(?:Server\.HTMLEncode|Server\.URLEncode|HttpUtility\.HtmlEncode|HttpUtility\.UrlEncode" +
        @"|WebUtility\.HtmlEncode|AntiXssEncoder\.HtmlEncode|CInt|CLng|CDbl|CSng|CCur|CBool" +
        @"|int\.Parse|long\.Parse|decimal\.Parse|Convert\.ToInt32|Convert\.ToInt64|Convert\.ToDecimal)\s*\(",
        RegexOptions.IgnoreCase)]
    private static partial Regex SafeCall();

    [GeneratedRegex(@"(?<![\w.])[A-Za-z_][\w.]*\s*\(")]
    private static partial Regex AnyCall();
}
