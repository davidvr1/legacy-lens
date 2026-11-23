using LegacyLens.Core.Evaluation.Rules;
using LegacyLens.Core.Rules;
using Xunit.Abstractions;

namespace LegacyLens.Core.Tests.Evaluation.Rules;

/// <summary>Runs the real rule engine over evals/rules/* (SPEC section 6, level 1).</summary>
public sealed class RuleEvalTests(ITestOutputHelper output)
{
    private static readonly string[] RuleIds = ["COM001", "ERR001", "INC001", "SEC001", "SES001", "SQL001", "XSS001"];

    private static string RulesFolder => Path.Combine(AppContext.BaseDirectory, "evals", "rules");

    private static IReadOnlyList<RuleEvalCase> LoadCases()
    {
        var cases = new List<RuleEvalCase>();
        foreach (var dir in Directory.GetDirectories(RulesFolder).OrderBy(d => d, StringComparer.Ordinal))
        {
            var id = Path.GetFileName(dir);
            var inputs = Directory.GetFiles(dir, "input.*");
            Assert.True(inputs.Length == 1, $"Case {id} must have exactly one input.* file but has {inputs.Length}.");
            var expectedPath = Path.Combine(dir, "expected.json");
            Assert.True(File.Exists(expectedPath), $"Case {id} has no expected.json.");
            cases.Add(RuleEvalCaseParser.Parse(id, File.ReadAllText(inputs[0]), File.ReadAllText(expectedPath)));
        }

        return cases;
    }

    private static RuleEngine CreateEngine() => new(
    [
        new SqlConcatenationRule(),
        new XssOutputRule(),
        new HardCodedSecretRule(),
        new OnErrorResumeNextRule(),
        new ComObjectRule(),
        new SessionUsageRule(),
        new IncludeRule(),
    ]);

    private static RuleEvalResult Run() => RuleEvaluator.Run(CreateEngine(), LoadCases());

    private static string Format(double? value) => value is null ? "n/a" : value.Value.ToString("0.000");

    [Fact]
    public void There_are_12_cases_with_unique_ids()
    {
        var cases = LoadCases();
        Assert.Equal(12, cases.Count);
        Assert.Equal(12, cases.Select(c => c.Id).Distinct().Count());
    }

    [Fact]
    public void Every_expected_rule_id_is_a_known_rule()
    {
        foreach (var c in LoadCases())
        {
            foreach (var f in c.Expected)
            {
                Assert.True(RuleIds.Contains(f.RuleId), $"Case {c.Id}: unknown rule id '{f.RuleId}'.");
            }
        }
    }

    [Fact]
    public void Engine_meets_precision_and_recall_thresholds()
    {
        var result = Run();

        output.WriteLine("Rule    TP  FP  FN  Precision  Recall");
        foreach (var r in result.PerRule.Append(result.Total))
        {
            output.WriteLine($"{r.RuleId,-7} {r.TruePositives,3} {r.FalsePositives,3} {r.FalseNegatives,3}  {Format(r.Precision),9}  {Format(r.Recall),6}");
        }

        foreach (var c in result.PerCase)
        {
            foreach (var m in c.Missed)
            {
                output.WriteLine($"MISSED     {c.CaseId}: {m.RuleId}:{m.Line}");
            }

            foreach (var u in c.Unexpected)
            {
                output.WriteLine($"UNEXPECTED {c.CaseId}: {u.RuleId}:{u.Line}");
            }
        }

        Assert.True(result.Total.Precision >= 0.95, $"Overall precision was {Format(result.Total.Precision)}");
        Assert.True(result.Total.Recall >= 0.9, $"Overall recall was {Format(result.Total.Recall)}");
    }

    [Fact]
    public void Known_limits_case_misses_exactly_xss001_on_line_9()
    {
        // Documents a known limit. If the rule learns to follow that value, update this test on purpose.
        var result = Run();
        var c = Assert.Single(result.PerCase, x => x.CaseId == "known-limits");
        Assert.Equal([new ExpectedRuleFinding("XSS001", 9)], c.Missed);
    }
}
