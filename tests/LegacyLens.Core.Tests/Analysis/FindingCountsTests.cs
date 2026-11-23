using LegacyLens.Core.Analysis;
using LegacyLens.Core.Model;

namespace LegacyLens.Core.Tests.Analysis;

public sealed class FindingCountsTests
{
    [Fact]
    public void From_counts_each_severity_and_is_zero_for_severities_without_findings()
    {
        var findings = new[]
        {
            new Finding("SQL001", Severity.High, 1, "t", "e", "f"),
            new Finding("XSS001", Severity.High, 2, "t", "e", "f"),
            new Finding("ERR001", Severity.Medium, 3, "t", "e", "f"),
            new Finding("INC001", Severity.Info, 4, "t", "e", "f"),
        };

        var counts = FindingCounts.From(findings);

        Assert.Equal(new FindingCounts(2, 1, 0, 1), counts);
    }

    [Fact]
    public void From_returns_all_zeros_for_no_findings()
    {
        Assert.Equal(new FindingCounts(0, 0, 0, 0), FindingCounts.From([]));
    }
}
