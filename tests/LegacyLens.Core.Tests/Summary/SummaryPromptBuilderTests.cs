using LegacyLens.Core.Model;
using LegacyLens.Core.Summary;

namespace LegacyLens.Core.Tests.Summary;

public sealed class SummaryPromptBuilderTests
{
    [Fact]
    public void Build_contains_file_name_and_classic_asp_language()
    {
        var prompt = SummaryPromptBuilder.Build(SummaryTestData.File(), []);
        Assert.Contains("page.asp", prompt);
        Assert.Contains("Classic ASP (VBScript)", prompt);
        Assert.DoesNotContain("ASP.NET WebForms", prompt);
    }

    [Fact]
    public void Build_contains_webforms_language()
    {
        var prompt = SummaryPromptBuilder.Build(SummaryTestData.File(SourceLanguage.WebForms, "a\nb"), []);
        Assert.Contains("ASP.NET WebForms", prompt);
        Assert.DoesNotContain("Classic ASP (VBScript)", prompt);
    }

    [Fact]
    public void Build_numbers_lines_from_one()
    {
        var prompt = SummaryPromptBuilder.Build(SummaryTestData.File(code: "first\nsecond\nthird\nfourth"), []);
        Assert.Contains("1: first", prompt);
        Assert.Contains("2: second", prompt);
        Assert.Contains("4: fourth", prompt);
        Assert.DoesNotContain("0: first", prompt);
    }

    [Fact]
    public void Build_lists_findings()
    {
        var f = new Finding("SQL001", Severity.High, 20, "SQL built by string concatenation", "e", "f");
        var prompt = SummaryPromptBuilder.Build(SummaryTestData.File(), [f]);
        Assert.Contains("- SQL001 (High) line 20: SQL built by string concatenation", prompt);
        Assert.DoesNotContain("- none", prompt);
    }

    [Fact]
    public void Build_says_none_without_findings()
    {
        Assert.Contains("- none", SummaryPromptBuilder.Build(SummaryTestData.File(), []));
    }

    [Fact]
    public void Build_contains_schema_and_json_only_instruction()
    {
        var prompt = SummaryPromptBuilder.Build(SummaryTestData.File(), []);
        Assert.Contains(CodeSummarySchema.Json, prompt);
        Assert.Contains("JSON only", prompt);
        Assert.Contains("ONLY", prompt);
    }

    [Fact]
    public void BuildRetry_contains_original_prompt_and_error()
    {
        var original = SummaryPromptBuilder.Build(SummaryTestData.File(), []);
        var retry = SummaryPromptBuilder.BuildRetry(original, "purpose is required.");
        Assert.Contains(original, retry);
        Assert.Contains("purpose is required.", retry);
        Assert.True(retry.Length > original.Length);
    }
}
