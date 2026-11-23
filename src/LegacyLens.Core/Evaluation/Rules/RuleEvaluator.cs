using LegacyLens.Core.Model;
using LegacyLens.Core.Rules;

namespace LegacyLens.Core.Evaluation.Rules;

/// <summary>Runs the rule engine on every eval case and aggregates precision and recall per rule.</summary>
public static class RuleEvaluator
{
    /// <summary>Runs each case through the engine. Each case is analyzed as a file named after its id.</summary>
    public static RuleEvalResult Run(IRuleEngine engine, IReadOnlyList<RuleEvalCase> cases)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(cases);

        var perCase = new List<RuleCaseResult>();
        var allExpected = new List<ExpectedRuleFinding>();
        var allMissed = new List<ExpectedRuleFinding>();
        var allUnexpected = new List<ExpectedRuleFinding>();

        foreach (var evalCase in cases)
        {
            var file = new SourceFile(evalCase.Id, evalCase.Language, evalCase.Code);
            var result = RuleMetrics.Compare(evalCase.Id, evalCase.Expected, engine.Run(file));

            perCase.Add(result);
            allExpected.AddRange(evalCase.Expected);
            allMissed.AddRange(result.Missed);
            allUnexpected.AddRange(result.Unexpected);
        }

        var perRule = RuleMetrics.Count(allExpected, allMissed, allUnexpected);
        var total = new RuleCounts(
            "ALL",
            perRule.Sum(counts => counts.TruePositives),
            perRule.Sum(counts => counts.FalsePositives),
            perRule.Sum(counts => counts.FalseNegatives));

        return new RuleEvalResult(cases.Count, perRule, total, perCase);
    }
}
