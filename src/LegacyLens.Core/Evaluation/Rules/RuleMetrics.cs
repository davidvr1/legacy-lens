using LegacyLens.Core.Model;

namespace LegacyLens.Core.Evaluation.Rules;

/// <summary>Compares expected rule findings with the actual ones. Findings match on exact (rule id, line) as a multiset.</summary>
public static class RuleMetrics
{
    /// <summary>Splits the findings of one case into missed (expected, not reported) and unexpected (reported, not expected).</summary>
    public static RuleCaseResult Compare(string caseId, IReadOnlyList<ExpectedRuleFinding> expected, IReadOnlyList<Finding> actual)
    {
        ArgumentNullException.ThrowIfNull(caseId);
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(actual);

        // How many expected copies of each finding are still waiting for an actual match.
        var unmatched = new Dictionary<ExpectedRuleFinding, int>();
        foreach (var finding in expected)
        {
            unmatched[finding] = unmatched.GetValueOrDefault(finding) + 1;
        }

        var unexpected = new List<ExpectedRuleFinding>();
        foreach (var finding in actual)
        {
            var key = new ExpectedRuleFinding(finding.RuleId, finding.Line);
            if (unmatched.GetValueOrDefault(key) > 0)
            {
                unmatched[key]--;
            }
            else
            {
                unexpected.Add(key);
            }
        }

        // Copies that are still unmatched are the missed ones. Equal copies are interchangeable, so the first ones in order are taken.
        var missed = new List<ExpectedRuleFinding>();
        foreach (var finding in expected)
        {
            if (unmatched.GetValueOrDefault(finding) > 0)
            {
                unmatched[finding]--;
                missed.Add(finding);
            }
        }

        return new RuleCaseResult(caseId, missed, unexpected);
    }

    /// <summary>
    /// Counts per rule over all cases: expected findings, missed (false negatives) and unexpected (false positives).
    /// True positives are expected minus missed. The result is sorted by rule id and has every rule that appears in any list.
    /// </summary>
    public static IReadOnlyList<RuleCounts> Count(
        IEnumerable<ExpectedRuleFinding> expected,
        IEnumerable<ExpectedRuleFinding> missed,
        IEnumerable<ExpectedRuleFinding> unexpected)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(missed);
        ArgumentNullException.ThrowIfNull(unexpected);

        var expectedByRule = CountByRule(expected);
        var missedByRule = CountByRule(missed);
        var unexpectedByRule = CountByRule(unexpected);

        var ruleIds = expectedByRule.Keys
            .Union(missedByRule.Keys, StringComparer.Ordinal)
            .Union(unexpectedByRule.Keys, StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal);

        return ruleIds
            .Select(ruleId =>
            {
                var falseNegatives = missedByRule.GetValueOrDefault(ruleId);
                var truePositives = expectedByRule.GetValueOrDefault(ruleId) - falseNegatives;
                return new RuleCounts(ruleId, truePositives, unexpectedByRule.GetValueOrDefault(ruleId), falseNegatives);
            })
            .ToList();
    }

    private static Dictionary<string, int> CountByRule(IEnumerable<ExpectedRuleFinding> findings)
    {
        return findings
            .GroupBy(finding => finding.RuleId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
    }
}
