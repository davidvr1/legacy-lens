using LegacyLens.Core.Evaluation.Rules;
using LegacyLens.Core.Model;
using LegacyLens.Core.Rules;

namespace LegacyLens.Core.Tests.Evaluation.Rules;

public sealed class RuleEvaluatorTests
{
    /// <summary>Reports a fixed finding for every line (1-based) that contains the trigger text.</summary>
    private sealed class FakeRule(string id, string trigger) : IRule
    {
        public string Id => id;

        public IReadOnlyList<Finding> Analyze(SourceFile file) =>
            file.Lines
                .Select((text, index) => (text, line: index + 1))
                .Where(x => x.text.Contains(trigger, StringComparison.Ordinal))
                .Select(x => new Finding(id, Severity.Low, x.line, "t", "e", "f"))
                .ToList();
    }

    private static RuleEngine Engine() => new([new FakeRule("AAA001", "aaa"), new FakeRule("BBB001", "bbb")]);

    private static RuleEvalCase Case(string id, string code, params ExpectedRuleFinding[] expected) =>
        new(id, SourceLanguage.ClassicAsp, code, expected);

    [Fact]
    public void Counts_per_rule_total_and_per_case()
    {
        var cases = new[]
        {
            // BBB expected at 2 but reported at 4 -> one miss, one false positive.
            Case("one", "aaa\nx\naaa\nbbb", new ExpectedRuleFinding("AAA001", 1), new ExpectedRuleFinding("AAA001", 3), new ExpectedRuleFinding("BBB001", 2)),
            Case("two", "bbb", new ExpectedRuleFinding("BBB001", 1)),
            // AAA expected at 1 and 2, reported only at 2.
            Case("three", "zzz\naaa", new ExpectedRuleFinding("AAA001", 1), new ExpectedRuleFinding("AAA001", 2)),
        };

        var result = RuleEvaluator.Run(Engine(), cases);

        Assert.Equal(3, result.Cases);
        Assert.Equal(["AAA001", "BBB001"], result.PerRule.Select(r => r.RuleId));

        // AAA: expected 4; actual: one -> 1,3; three -> 2 (line 1 of "three" is zzz, so AAA@1 is missed). TP=3, FN=1.
        Assert.Equal(new RuleCounts("AAA001", 3, 0, 1), result.PerRule[0]);
        // BBB: expected 2; actual one:4, two:1. TP=1, FN=1, FP=1.
        Assert.Equal(new RuleCounts("BBB001", 1, 1, 1), result.PerRule[1]);

        Assert.Equal(new RuleCounts("ALL", 4, 1, 2), result.Total);

        Assert.Equal(["one", "two", "three"], result.PerCase.Select(c => c.CaseId));
        Assert.Equal([new ExpectedRuleFinding("BBB001", 2)], result.PerCase[0].Missed);
        Assert.Equal([new ExpectedRuleFinding("BBB001", 4)], result.PerCase[0].Unexpected);
        Assert.Empty(result.PerCase[1].Missed);
        Assert.Empty(result.PerCase[1].Unexpected);
        Assert.Equal([new ExpectedRuleFinding("AAA001", 1)], result.PerCase[2].Missed);
        Assert.Empty(result.PerCase[2].Unexpected);
    }

    [Fact]
    public void Per_rule_is_sorted_by_rule_id_even_when_cases_list_them_in_another_order()
    {
        var result = RuleEvaluator.Run(Engine(), [Case("c", "bbb\naaa", new ExpectedRuleFinding("BBB001", 1), new ExpectedRuleFinding("AAA001", 2))]);
        Assert.Equal(["AAA001", "BBB001"], result.PerRule.Select(r => r.RuleId));
    }

    [Fact]
    public void No_cases_gives_empty_result_with_zero_total()
    {
        var result = RuleEvaluator.Run(Engine(), []);
        Assert.Equal(0, result.Cases);
        Assert.Empty(result.PerRule);
        Assert.Empty(result.PerCase);
        Assert.Equal(new RuleCounts("ALL", 0, 0, 0), result.Total);
        Assert.Null(result.Total.Precision);
        Assert.Null(result.Total.Recall);
    }

    [Fact]
    public void Unexpected_finding_of_a_rule_with_no_expectations_appears_in_per_rule()
    {
        var result = RuleEvaluator.Run(Engine(), [Case("c", "aaa")]);
        Assert.Equal(new RuleCounts("AAA001", 0, 1, 0), Assert.Single(result.PerRule));
        Assert.Equal(new RuleCounts("ALL", 0, 1, 0), result.Total);
    }
}
