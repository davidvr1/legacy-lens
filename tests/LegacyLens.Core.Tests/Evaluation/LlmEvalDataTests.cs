using LegacyLens.Core.Evaluation;

namespace LegacyLens.Core.Tests.Evaluation;

/// <summary>Sanity checks over the real evals/llm/ folder: the cases parse and their expected facts are real.</summary>
public sealed class LlmEvalDataTests
{
    private static string EvalDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "LegacyLens.sln")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "evals", "llm");
    }

    private static IReadOnlyList<string> CaseDirs() => Directory.GetDirectories(EvalDir()).OrderBy(d => d, StringComparer.Ordinal).ToList();

    private static LlmEvalCase Load(string caseDir)
    {
        var input = Assert.Single(Directory.GetFiles(caseDir, "input.*"));
        return LlmEvalCaseParser.Parse(
            Path.GetFileName(caseDir),
            Path.GetFileName(input),
            File.ReadAllText(input),
            File.ReadAllText(Path.Combine(caseDir, "expected.json")));
    }

    [Fact]
    public void Between_six_and_eight_cases()
    {
        var count = CaseDirs().Count;
        Assert.InRange(count, 6, 8);
    }

    [Fact]
    public void Every_case_has_expected_json_and_exactly_one_input_file_and_parses()
    {
        foreach (var dir in CaseDirs())
        {
            Assert.True(File.Exists(Path.Combine(dir, "expected.json")), $"{dir}: expected.json missing");
            Assert.Single(Directory.GetFiles(dir, "input.*"));
            var c = Load(dir);
            Assert.NotEmpty(c.Facts);
            Assert.False(string.IsNullOrWhiteSpace(c.File.Code), $"{dir}: input is empty");
        }
    }

    [Fact]
    public void Expected_facts_for_inputs_includes_com_objects_and_database_occur_in_the_code()
    {
        var checkedCategories = new[] { SummaryCategory.Inputs, SummaryCategory.Includes, SummaryCategory.ComObjects, SummaryCategory.DatabaseAccess };
        var problems = new List<string>();

        foreach (var dir in CaseDirs())
        {
            var c = Load(dir);
            foreach (var fact in c.Facts.Where(f => checkedCategories.Contains(f.Category)))
            {
                foreach (var keyword in fact.Keywords)
                {
                    var found = keyword.Split('|').Any(alt => c.File.Code.Contains(alt.Trim(), StringComparison.OrdinalIgnoreCase));
                    if (!found)
                    {
                        problems.Add($"{c.Id}: {fact.Category} keyword '{keyword}' not in code");
                    }
                }
            }
        }

        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }
}
