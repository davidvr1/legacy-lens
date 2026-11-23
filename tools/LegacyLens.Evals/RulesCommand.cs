using System.Globalization;
using System.Text.Json;
using LegacyLens.Core.Evaluation.Rules;
using LegacyLens.Core.Rules;

namespace LegacyLens.Evals;

/// <summary>Runs the rule eval over evals/rules and prints precision and recall per rule as Markdown.</summary>
internal static class RulesCommand
{
    public static async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        var root = RepoRoot.Find(Directory.GetCurrentDirectory());
        if (root is null)
        {
            Console.Error.WriteLine("LegacyLens.sln was not found in the current directory or any parent. Run the command inside the repository.");
            return 1;
        }

        var casesPath = Path.Combine(root, "evals", "rules");
        if (!Directory.Exists(casesPath))
        {
            Console.Error.WriteLine($"Rule eval folder not found: '{casesPath}'.");
            return 1;
        }

        IReadOnlyList<RuleEvalCase> cases;
        try
        {
            cases = await LoadCasesAsync(casesPath, cancellationToken);
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException)
        {
            Console.Error.WriteLine($"Invalid rule eval case in '{casesPath}': {ex.Message}");
            return 1;
        }

        var engine = new RuleEngine(
        [
            new SqlConcatenationRule(),
            new XssOutputRule(),
            new HardCodedSecretRule(),
            new OnErrorResumeNextRule(),
            new ComObjectRule(),
            new SessionUsageRule(),
            new IncludeRule(),
        ]);

        var result = RuleEvaluator.Run(engine, cases);

        Console.WriteLine("| Rule | TP | FP | FN | Precision | Recall |");
        Console.WriteLine("|---|---|---|---|---|---|");
        foreach (var counts in result.PerRule)
        {
            Console.WriteLine(Row(counts.RuleId, counts));
        }

        Console.WriteLine(Row("**All**", result.Total));

        Console.WriteLine();
        Console.WriteLine("Misses:");
        var anyMiss = false;
        foreach (var caseResult in result.PerCase)
        {
            var parts = new List<string>();
            if (caseResult.Missed.Count > 0)
            {
                parts.Add("missed " + Describe(caseResult.Missed));
            }

            if (caseResult.Unexpected.Count > 0)
            {
                parts.Add("unexpected " + Describe(caseResult.Unexpected));
            }

            if (parts.Count > 0)
            {
                anyMiss = true;
                Console.WriteLine($"- {caseResult.CaseId}: {string.Join("; ", parts)}");
            }
        }

        if (!anyMiss)
        {
            Console.WriteLine("- none");
        }

        return 0;
    }

    /// <summary>Reads every folder of evals/rules (ordered by name) as one case: one input.* file and expected.json.</summary>
    private static async Task<IReadOnlyList<RuleEvalCase>> LoadCasesAsync(string casesPath, CancellationToken cancellationToken)
    {
        var cases = new List<RuleEvalCase>();
        var folders = new DirectoryInfo(casesPath).GetDirectories().OrderBy(folder => folder.Name, StringComparer.Ordinal);
        foreach (var folder in folders)
        {
            var inputs = folder.GetFiles("input.*");
            if (inputs.Length != 1)
            {
                throw new InvalidDataException($"Case '{folder.Name}' must have exactly one input.* file, found {inputs.Length}.");
            }

            var expectedPath = Path.Combine(folder.FullName, "expected.json");
            if (!File.Exists(expectedPath))
            {
                throw new InvalidDataException($"Case '{folder.Name}' has no expected.json.");
            }

            var code = await File.ReadAllTextAsync(inputs[0].FullName, cancellationToken);
            var expectedJson = await File.ReadAllTextAsync(expectedPath, cancellationToken);
            cases.Add(RuleEvalCaseParser.Parse(folder.Name, code, expectedJson));
        }

        return cases;
    }

    private static string Row(string name, RuleCounts counts) =>
        $"| {name} | {counts.TruePositives} | {counts.FalsePositives} | {counts.FalseNegatives} | {Format(counts.Precision)} | {Format(counts.Recall)} |";

    private static string Describe(IEnumerable<ExpectedRuleFinding> findings) =>
        string.Join(", ", findings.Select(finding => $"{finding.RuleId}:{finding.Line.ToString(CultureInfo.InvariantCulture)}"));

    private static string Format(double? value) => value is null ? "–" : value.Value.ToString("F3", CultureInfo.InvariantCulture);
}
