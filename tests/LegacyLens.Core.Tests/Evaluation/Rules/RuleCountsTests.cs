using LegacyLens.Core.Evaluation.Rules;

namespace LegacyLens.Core.Tests.Evaluation.Rules;

public sealed class RuleCountsTests
{
    [Fact]
    public void Precision_and_recall_values()
    {
        var c = new RuleCounts("R", 3, 1, 2);
        Assert.Equal(0.75, c.Precision!.Value, 9);
        Assert.Equal(0.6, c.Recall!.Value, 9);
    }

    [Fact]
    public void Perfect_counts_give_one()
    {
        var c = new RuleCounts("R", 4, 0, 0);
        Assert.Equal(1.0, c.Precision);
        Assert.Equal(1.0, c.Recall);
    }

    [Fact]
    public void Zero_true_positives_with_denominator_gives_zero()
    {
        var c = new RuleCounts("R", 0, 2, 3);
        Assert.Equal(0.0, c.Precision);
        Assert.Equal(0.0, c.Recall);
    }

    [Fact]
    public void Precision_is_null_when_nothing_reported()
    {
        var c = new RuleCounts("R", 0, 0, 5);
        Assert.Null(c.Precision);
        Assert.Equal(0.0, c.Recall);
    }

    [Fact]
    public void Recall_is_null_when_nothing_expected()
    {
        var c = new RuleCounts("R", 0, 5, 0);
        Assert.Equal(0.0, c.Precision);
        Assert.Null(c.Recall);
    }

    [Fact]
    public void All_zero_gives_null_for_both()
    {
        var c = new RuleCounts("R", 0, 0, 0);
        Assert.Null(c.Precision);
        Assert.Null(c.Recall);
    }
}
