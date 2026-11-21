using System.Text.RegularExpressions;
using LegacyLens.Core.Model;
using LegacyLens.Core.Rules.Parsing;

namespace LegacyLens.Core.Rules;

/// <summary>
/// ERR001: <c>On Error Resume Next</c> with no <c>Err.Number</c> check before error handling is
/// changed again (<c>On Error GoTo 0</c>, another <c>On Error</c>) or the file ends.
/// </summary>
public sealed partial class OnErrorResumeNextRule : IRule
{
    public string Id => "ERR001";

    public IReadOnlyList<Finding> Analyze(SourceFile file)
    {
        var lines = file.CodeLines.Select(line => StringLiterals.Split(line, file.Language).Masked).ToList();
        var findings = new List<Finding>();

        for (var i = 0; i < lines.Count; i++)
        {
            if (ResumeNext().IsMatch(lines[i]) && !IsChecked(lines, i + 1))
            {
                findings.Add(new Finding(
                    Id,
                    Severity.Medium,
                    i + 1,
                    "Errors are silently ignored",
                    "'On Error Resume Next' is used without a later Err.Number check. Failed database calls or other errors are ignored and the page continues with bad data.",
                    "Check Err.Number after each risky call, or remove 'On Error Resume Next'. In ASP.NET Core, use exceptions with try/catch and the global exception handler."));
            }
        }

        return findings;
    }

    private static bool IsChecked(IReadOnlyList<string> lines, int start)
    {
        for (var i = start; i < lines.Count; i++)
        {
            if (ErrorCheck().IsMatch(lines[i]))
            {
                return true;
            }

            if (OnErrorStatement().IsMatch(lines[i]))
            {
                return false;
            }
        }

        return false;
    }

    [GeneratedRegex(@"\bOn\s+Error\s+Resume\s+Next\b", RegexOptions.IgnoreCase)]
    private static partial Regex ResumeNext();

    [GeneratedRegex(@"\bOn\s+Error\b", RegexOptions.IgnoreCase)]
    private static partial Regex OnErrorStatement();

    [GeneratedRegex(@"\bErr\s*\.\s*Number\b|\bIf\s+(?:Not\s+)?Err\b", RegexOptions.IgnoreCase)]
    private static partial Regex ErrorCheck();
}
