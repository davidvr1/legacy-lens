using System.Text.RegularExpressions;
using LegacyLens.Core.Model;

namespace LegacyLens.Core.Rules;

/// <summary>INC001: lists server-side include dependencies (&lt;!--#include file/virtual="..."--&gt;).</summary>
public sealed partial class IncludeRule : IRule
{
    public string Id => "INC001";

    public IReadOnlyList<Finding> Analyze(SourceFile file)
    {
        var findings = new List<Finding>();

        // Includes are HTML comments, so they are read from the raw lines.
        for (var i = 0; i < file.Lines.Count; i++)
        {
            foreach (Match match in Include().Matches(file.Lines[i]))
            {
                var path = match.Groups["path"].Value;
                findings.Add(new Finding(
                    Id,
                    Severity.Info,
                    i + 1,
                    $"Include dependency: {path}",
                    $"The page includes '{path}' (#include {match.Groups["kind"].Value.ToLowerInvariant()}). The included code runs as part of this page and must be migrated with it.",
                    "Move shared markup to a Razor layout or partial view, and shared logic to a service registered with dependency injection."));
            }
        }

        return findings;
    }

    [GeneratedRegex("""<!--\s*#include\s+(?<kind>file|virtual)\s*=\s*"(?<path>[^"]+)"\s*-->""", RegexOptions.IgnoreCase)]
    private static partial Regex Include();
}
