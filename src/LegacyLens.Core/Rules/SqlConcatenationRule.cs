using System.Text.RegularExpressions;
using LegacyLens.Core.Model;
using LegacyLens.Core.Rules.Parsing;

namespace LegacyLens.Core.Rules;

/// <summary>
/// SQL001: SQL text built by concatenating request data or variables (SQL injection).
/// A line is checked when a string literal on it looks like SQL, or when it appends to a
/// variable that was earlier assigned SQL text, for example <c>sql = sql &amp; " AND x = " &amp; y</c>.
/// </summary>
public sealed partial class SqlConcatenationRule : IRule
{
    // Operands that cannot carry injected SQL: line continuation, string constants, numeric conversions.
    private static readonly HashSet<string> SafeOperands = new(StringComparer.OrdinalIgnoreCase)
    {
        "_", "vbCrLf", "vbCr", "vbLf", "vbTab", "vbNewLine", "Environment.NewLine", "string.Empty",
        "CInt", "CLng", "CDbl", "CSng", "CCur", "CBool",
        "int.Parse", "long.Parse", "decimal.Parse", "Convert.ToInt32", "Convert.ToInt64", "Convert.ToDecimal",
    };

    public string Id => "SQL001";

    public IReadOnlyList<Finding> Analyze(SourceFile file)
    {
        var findings = new List<Finding>();
        var sqlVariables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < file.CodeLines.Count; i++)
        {
            var (masked, literals) = StringLiterals.Split(file.CodeLines[i], file.Language);
            if (CodePatterns.Output().IsMatch(masked))
            {
                continue; // Text written to the page is XSS001's concern, not SQL.
            }

            var target = Assignment.TryParse(masked)?.Target;
            var hasSqlText = literals.Any(literal => SqlText().IsMatch(literal));
            var extendsSqlVariable = target is not null && sqlVariables.Contains(target);

            if (hasSqlText || extendsSqlVariable)
            {
                var operands = ConcatenatedOperands(masked)
                    .Where(op => !SafeOperands.Contains(op))
                    .Where(op => !string.Equals(op, target, StringComparison.OrdinalIgnoreCase))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (operands.Count > 0)
                {
                    var fromRequest = CodePatterns.RequestData().IsMatch(masked);
                    findings.Add(CreateFinding(i + 1, operands, fromRequest));
                }
            }

            if (hasSqlText && target is not null)
            {
                sqlVariables.Add(target);
            }
        }

        return findings;
    }

    private static IEnumerable<string> ConcatenatedOperands(string masked)
    {
        foreach (Match match in LeftOperand().Matches(masked))
        {
            yield return match.Groups["op"].Value;
        }

        foreach (Match match in RightOperand().Matches(masked))
        {
            yield return match.Groups["op"].Value;
        }
    }

    private Finding CreateFinding(int line, IReadOnlyList<string> operands, bool fromRequest)
    {
        var list = string.Join(", ", operands);
        var explanation = fromRequest
            ? $"Request data ({list}) is concatenated directly into a SQL statement. An attacker can change the query (SQL injection)."
            : $"The SQL statement is built by concatenating {list}. If any of these values comes from user input, an attacker can change the query (SQL injection).";

        return new Finding(
            Id,
            Severity.High,
            line,
            "SQL built by string concatenation",
            explanation,
            "Use a parameterized query: ADODB.Command with Parameters in Classic ASP; SqlParameter, Dapper or EF Core in .NET. Never concatenate values into SQL text.");
    }

    // SQL-looking text inside a string literal. Fragments like " AND Name = '" count too.
    [GeneratedRegex(
        @"\bselect\b.+\bfrom\b|\binsert\s+into\b|\bupdate\s+\[?\w+\]?\s+set\b|\bdelete\s+from\b" +
        @"|\b(?:where|and|or)\s+[\w.\[\]]+\s*(?:=|<>|<|>|\blike\b|\bin\b)" +
        @"|\bset\s+[\w\[\]]+\s*=|\bvalues\s*\(|\border\s+by\b|\bexec(?:ute)?\s+\w+",
        RegexOptions.IgnoreCase)]
    private static partial Regex SqlText();

    // An identifier (optionally followed by one call or indexer) before & or +, but not &=, +=, &&, ++.
    [GeneratedRegex(@"(?<![\w.])(?<op>[A-Za-z_][\w.]*)\s*(?:\((?:[^()]|\([^()]*\))*\)|\[[^\[\]]*\])?\s*[&+](?![=&+])")]
    private static partial Regex LeftOperand();

    // An identifier after & or +.
    [GeneratedRegex(@"(?<![&+])[&+](?![=&+])\s*(?<op>[A-Za-z_][\w.]*)")]
    private static partial Regex RightOperand();
}
