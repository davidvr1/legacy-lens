using LegacyLens.Core.Llm;
using LegacyLens.Core.Model;
using LegacyLens.Core.Summary;

namespace LegacyLens.Core.Tests.Summary;

public sealed class SummaryPromptVariantTests
{
    private static string FewShot() => SummaryPromptBuilder.Build(SummaryTestData.File(), [], PromptVariant.FewShot);

    [Fact]
    public void ZeroShot_has_no_example()
    {
        var prompt = SummaryPromptBuilder.Build(SummaryTestData.File(), [], PromptVariant.ZeroShot);
        Assert.DoesNotContain(SummaryPromptBuilder.FewShotExampleHeader, prompt);
        Assert.DoesNotContain("Now the real file.", prompt);
        Assert.DoesNotContain("header.inc", prompt);
    }

    [Fact]
    public void Default_variant_equals_ZeroShot()
    {
        Assert.Equal(
            SummaryPromptBuilder.Build(SummaryTestData.File(), []),
            SummaryPromptBuilder.Build(SummaryTestData.File(), [], PromptVariant.ZeroShot));
    }

    [Fact]
    public void FewShot_has_header_text()
    {
        Assert.Equal(
            "Example (a different file, only to show the expected style; do not copy its facts):",
            SummaryPromptBuilder.FewShotExampleHeader);
        Assert.Contains(SummaryPromptBuilder.FewShotExampleHeader, FewShot());
    }

    [Fact]
    public void FewShot_contains_numbered_example_code()
    {
        var prompt = FewShot();
        Assert.Contains("1: <!--#include file=\"header.inc\"-->", prompt);
        Assert.Contains("2: <%", prompt);
        Assert.Contains("4: id = Request.QueryString(\"productId\")", prompt);
        Assert.Contains("8: %>", prompt);
    }

    [Fact]
    public void FewShot_contains_example_json_with_readable_angle_brackets()
    {
        var prompt = FewShot();
        Assert.Contains("JSON reply:", prompt);
        Assert.Contains("\"purpose\":\"Shows the name of one product, looked up by the productId query string value.\"", prompt);
        Assert.Contains("\"inputs\":[\"QueryString: productId\",\"Application: Db\"]", prompt);
        Assert.Contains("\"comObjects\":[\"ADODB.Recordset\"]", prompt);
        Assert.Contains("\"includes\":[\"header.inc\"]", prompt);
        Assert.DoesNotContain("\\u003C", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FewShot_now_the_real_file_comes_before_file_name_and_after_example()
    {
        var prompt = FewShot();
        var header = prompt.IndexOf(SummaryPromptBuilder.FewShotExampleHeader, StringComparison.Ordinal);
        var json = prompt.IndexOf("JSON reply:", StringComparison.Ordinal);
        var now = prompt.IndexOf("Now the real file.", StringComparison.Ordinal);
        var fileName = prompt.IndexOf("File name:", StringComparison.Ordinal);
        Assert.True(header >= 0 && header < json && json < now && now < fileName);
    }

    [Fact]
    public void FewShot_is_zero_shot_plus_inserted_section_before_file_name()
    {
        var zero = SummaryPromptBuilder.Build(SummaryTestData.File(), [], PromptVariant.ZeroShot);
        var few = FewShot();
        var at = zero.IndexOf("File name:", StringComparison.Ordinal);
        Assert.StartsWith(zero[..at], few);
        Assert.EndsWith(zero[at..], few);
    }

    [Fact]
    public void FewShot_still_contains_real_file_code()
    {
        var prompt = SummaryPromptBuilder.Build(SummaryTestData.File(code: "real1\nreal2"), [], PromptVariant.FewShot);
        Assert.Contains("1: real1", prompt);
        Assert.Contains("2: real2", prompt);
    }

    [Fact]
    public async Task Service_passes_configured_variant_FewShot()
    {
        var fake = new FakeLlmClient().Enqueue(SummaryTestData.Valid());
        await new SummaryService(fake, new SummarySettings(PromptVariant.FewShot))
            .SummarizeAsync(SummaryTestData.File(), [], CancellationToken.None);
        Assert.Contains(SummaryPromptBuilder.FewShotExampleHeader, Assert.Single(fake.Prompts));
    }

    [Fact]
    public async Task Service_passes_configured_variant_ZeroShot()
    {
        var fake = new FakeLlmClient().Enqueue(SummaryTestData.Valid());
        await new SummaryService(fake, new SummarySettings(PromptVariant.ZeroShot))
            .SummarizeAsync(SummaryTestData.File(), [], CancellationToken.None);
        Assert.DoesNotContain(SummaryPromptBuilder.FewShotExampleHeader, Assert.Single(fake.Prompts));
    }
}
