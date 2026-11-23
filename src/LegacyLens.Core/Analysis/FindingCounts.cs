using LegacyLens.Core.Model;

namespace LegacyLens.Core.Analysis;

/// <summary>How many findings an analysis has per severity.</summary>
public sealed record FindingCounts(int High, int Medium, int Low, int Info)
{
    public static FindingCounts From(IEnumerable<Finding> findings)
    {
        var counts = findings
            .GroupBy(f => f.Severity)
            .ToDictionary(g => g.Key, g => g.Count());

        return new FindingCounts(
            counts.GetValueOrDefault(Severity.High),
            counts.GetValueOrDefault(Severity.Medium),
            counts.GetValueOrDefault(Severity.Low),
            counts.GetValueOrDefault(Severity.Info));
    }
}
