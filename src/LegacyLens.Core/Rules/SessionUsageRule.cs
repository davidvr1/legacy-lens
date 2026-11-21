using System.Text.RegularExpressions;
using LegacyLens.Core.Model;
using LegacyLens.Core.Rules.Parsing;

namespace LegacyLens.Core.Rules;

/// <summary>SES001: heavy Session usage (more than <see cref="Threshold"/> reads and writes in one file).</summary>
public sealed partial class SessionUsageRule : IRule
{
    public const int DefaultThreshold = 10;

    public SessionUsageRule(int threshold = DefaultThreshold)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(threshold);
        Threshold = threshold;
    }

    public int Threshold { get; }

    public string Id => "SES001";

    public IReadOnlyList<Finding> Analyze(SourceFile file)
    {
        var count = 0;
        var firstLine = 0;
        var keys = new List<string>();

        for (var i = 0; i < file.CodeLines.Count; i++)
        {
            var masked = StringLiterals.Split(file.CodeLines[i], file.Language).Masked;
            var accesses = SessionAccess().Count(masked);
            if (accesses == 0)
            {
                continue;
            }

            count += accesses;
            firstLine = firstLine == 0 ? i + 1 : firstLine;
            keys.AddRange(SessionKey().Matches(file.CodeLines[i]).Select(match => match.Groups["key"].Value));
        }

        if (count <= Threshold)
        {
            return [];
        }

        var distinctKeys = keys.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return
        [
            new Finding(
                Id,
                Severity.Low,
                firstLine,
                "Heavy Session usage",
                $"The page reads or writes Session {count} times ({distinctKeys.Count} keys: {string.Join(", ", distinctKeys)}). " +
                "Heavy use of in-process session state hides dependencies between pages and makes migration and scaling harder.",
                "Group related values into a small typed object, pass data explicitly where possible, and use ASP.NET Core session or a distributed cache behind a typed service."),
        ];
    }

    [GeneratedRegex(@"\bSession\s*(?:\.\s*Contents\s*)?[\(\[]", RegexOptions.IgnoreCase)]
    private static partial Regex SessionAccess();

    [GeneratedRegex(@"\bSession\s*(?:\.\s*Contents\s*)?[\(\[]\s*""(?<key>[^""]*)""", RegexOptions.IgnoreCase)]
    private static partial Regex SessionKey();
}
