using LegacyLens.Core.Evaluation.Rules;
using LegacyLens.Core.Model;

namespace LegacyLens.Core.Tests.Evaluation.Rules;

public sealed class RuleMetricsTests
{
    private static Finding F(string id, int line) => new(id, Severity.Low, line, "t", "e", "f");

    private static ExpectedRuleFinding E(string id, int line) => new(id, line);

    [Fact]
    public void Exact_match_has_no_differences()
    {
        var r = RuleMetrics.Compare("c", [E("SQL001", 2), E("XSS001", 4)], [F("XSS001", 4), F("SQL001", 2)]);
        Assert.Equal("c", r.CaseId);
        Assert.Empty(r.Missed);
        Assert.Empty(r.Unexpected);
    }

    [Fact]
    public void Missing_finding_is_missed()
    {
        var r = RuleMetrics.Compare("c", [E("SQL001", 2), E("XSS001", 4)], [F("SQL001", 2)]);
        Assert.Equal([E("XSS001", 4)], r.Missed);
        Assert.Empty(r.Unexpected);
    }

    [Fact]
    public void Extra_finding_is_unexpected()
    {
        var r = RuleMetrics.Compare("c", [E("SQL001", 2)], [F("SQL001", 2), F("COM001", 7)]);
        Assert.Empty(r.Missed);
        Assert.Equal([E("COM001", 7)], r.Unexpected);
    }

    [Fact]
    public void Same_rule_on_different_line_is_missed_and_unexpected()
    {
        var r = RuleMetrics.Compare("c", [E("SQL001", 2)], [F("SQL001", 3)]);
        Assert.Equal([E("SQL001", 2)], r.Missed);
        Assert.Equal([E("SQL001", 3)], r.Unexpected);
    }

    [Fact]
    public void Different_rule_on_same_line_is_missed_and_unexpected()
    {
        var r = RuleMetrics.Compare("c", [E("SQL001", 2)], [F("XSS001", 2)]);
        Assert.Equal([E("SQL001", 2)], r.Missed);
        Assert.Equal([E("XSS001", 2)], r.Unexpected);
    }

    [Fact]
    public void Two_expected_on_same_line_with_one_actual_means_one_missed()
    {
        var r = RuleMetrics.Compare("c", [E("XSS001", 5), E("XSS001", 5)], [F("XSS001", 5)]);
        Assert.Equal([E("XSS001", 5)], r.Missed);
        Assert.Empty(r.Unexpected);
    }

    [Fact]
    public void One_expected_with_two_actual_on_same_line_means_one_unexpected()
    {
        var r = RuleMetrics.Compare("c", [E("XSS001", 5)], [F("XSS001", 5), F("XSS001", 5)]);
        Assert.Empty(r.Missed);
        Assert.Equal([E("XSS001", 5)], r.Unexpected);
    }

    [Fact]
    public void Empty_inputs_give_empty_result()
    {
        var r = RuleMetrics.Compare("c", [], []);
        Assert.Empty(r.Missed);
        Assert.Empty(r.Unexpected);
    }

    [Fact]
    public void Count_computes_tp_fp_fn_sorted_by_rule()
    {
        var expected = new[] { E("XSS001", 1), E("XSS001", 2), E("SQL001", 3) };
        var missed = new[] { E("XSS001", 2) };
        var unexpected = new[] { E("COM001", 9), E("SQL001", 4) };

        var counts = RuleMetrics.Count(expected, missed, unexpected);

        Assert.Equal(["COM001", "SQL001", "XSS001"], counts.Select(c => c.RuleId));
        Assert.Equal(new RuleCounts("COM001", 0, 1, 0), counts[0]);
        Assert.Equal(new RuleCounts("SQL001", 1, 1, 0), counts[1]);
        Assert.Equal(new RuleCounts("XSS001", 1, 0, 1), counts[2]);
    }

    [Fact]
    public void Count_of_nothing_is_empty()
    {
        Assert.Empty(RuleMetrics.Count([], [], []));
    }
}
