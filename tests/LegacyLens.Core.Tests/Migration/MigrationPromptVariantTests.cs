using LegacyLens.Core.Llm;
using LegacyLens.Core.Migration;
using LegacyLens.Core.Tests.Summary;
using static LegacyLens.Core.Tests.Migration.MigrationTestData;

namespace LegacyLens.Core.Tests.Migration;

public sealed class MigrationPromptVariantTests
{
    private static string Build(PromptVariant v) => MigrationPromptBuilder.Build(File(), [Finding()], [ChunkA, ChunkB], v);

    [Fact]
    public void ZeroShot_has_no_example()
    {
        var prompt = Build(PromptVariant.ZeroShot);
        Assert.DoesNotContain(MigrationPromptBuilder.FewShotExampleHeader, prompt);
        Assert.DoesNotContain("Now the real file.", prompt);
        Assert.DoesNotContain("example-note-id", prompt);
    }

    [Fact]
    public void FewShot_example_without_notes_cites_nothing()
    {
        var prompt = MigrationPromptBuilder.Build(File(), [], [], PromptVariant.FewShot);
        Assert.Contains("\"sources\":[]", prompt);
    }

    [Fact]
    public void Prompt_asks_to_escape_quotes_inside_the_code_string()
        => Assert.Contains("escape every double quote in the code as \\\"", Build(PromptVariant.ZeroShot));

    [Fact]
    public void Default_variant_equals_ZeroShot()
        => Assert.Equal(MigrationPromptBuilder.Build(File(), [Finding()], [ChunkA, ChunkB]), Build(PromptVariant.ZeroShot));

    [Fact]
    public void FewShot_has_header_text()
    {
        Assert.Equal(
            "Example (a different file, only to show the expected style; do not copy it):",
            MigrationPromptBuilder.FewShotExampleHeader);
        Assert.Contains(MigrationPromptBuilder.FewShotExampleHeader, Build(PromptVariant.FewShot));
    }

    [Fact]
    public void FewShot_contains_numbered_legacy_example_code()
    {
        var prompt = Build(PromptVariant.FewShot);
        Assert.Contains("1: <!--#include file=\"header.inc\"-->", prompt);
        Assert.Contains("7: Response.Write \"<h1>\" & rs(\"Name\") & \"</h1>\"", prompt);
    }

    [Fact]
    public void FewShot_json_has_readable_generic_brackets_and_cites_first_prompt_note()
    {
        var prompt = Build(PromptVariant.FewShot);
        Assert.Contains("Task<IActionResult>", prompt);
        Assert.DoesNotContain("\\u003C", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\\u003E", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"sources\":[\"sql-injection\"]", prompt);
        Assert.DoesNotContain("example-note-id", prompt);
        Assert.Contains("\"notes\":[\"The SQL query now uses a parameter instead of string concatenation.\",\"header.inc becomes the shared layout.\"]", prompt);
    }

    [Fact]
    public void FewShot_order_example_then_cite_line_then_now_real_file_then_file_name()
    {
        var prompt = Build(PromptVariant.FewShot);
        var header = prompt.IndexOf(MigrationPromptBuilder.FewShotExampleHeader, StringComparison.Ordinal);
        var json = prompt.IndexOf("JSON reply:", StringComparison.Ordinal);
        var cite = prompt.IndexOf("In the real reply, cite only ids from the Knowledge notes list.", StringComparison.Ordinal);
        var now = prompt.IndexOf("Now the real file.", StringComparison.Ordinal);
        var fileName = prompt.IndexOf("File name:", StringComparison.Ordinal);
        Assert.True(header >= 0 && header < json && json < cite && cite < now && now < fileName);
    }

    [Fact]
    public void FewShot_example_code_const_matches_contract()
    {
        Assert.StartsWith("using System.Data.Common;\nusing Microsoft.AspNetCore.Mvc;", MigrationPromptBuilder.FewShotExampleCode);
        Assert.Contains("namespace Migrated.Pages;", MigrationPromptBuilder.FewShotExampleCode);
        Assert.EndsWith("}", MigrationPromptBuilder.FewShotExampleCode);
    }

    [Fact]
    public void ParseSourceIds_identical_for_zero_shot_and_few_shot()
    {
        var zero = MigrationPromptBuilder.ParseSourceIds(Build(PromptVariant.ZeroShot));
        var few = MigrationPromptBuilder.ParseSourceIds(Build(PromptVariant.FewShot));
        Assert.Equal(["sql-injection", "xss-output"], zero);
        Assert.Equal(zero, few);
    }

    [Fact]
    public void ParseSourceIds_empty_chunks_few_shot_is_empty()
        => Assert.Empty(MigrationPromptBuilder.ParseSourceIds(MigrationPromptBuilder.Build(File(), [], [], PromptVariant.FewShot)));

    [Fact]
    public void FewShot_example_adds_no_line_starting_with_source_header_prefix()
    {
        var lines = Build(PromptVariant.FewShot).Split('\n');
        Assert.Equal(2, lines.Count(l => l.StartsWith(MigrationPromptBuilder.SourceHeaderPrefix, StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData(PromptVariant.FewShot, true)]
    [InlineData(PromptVariant.ZeroShot, false)]
    public async Task Service_passes_configured_variant(PromptVariant variant, bool expectExample)
    {
        var llm = new FakeLlmClient().Enqueue(Reply("class A {}", "sql-injection"));
        var service = new MigrationService(llm, new FakeRetriever([ChunkA]), new FakeCodeValidator(Ok()), new MigrationSettings(3, variant));

        await service.ProposeAsync(File(), [Finding()], CancellationToken.None);

        Assert.Equal(expectExample, Assert.Single(llm.Prompts).Contains(MigrationPromptBuilder.FewShotExampleHeader));
    }

    [Fact]
    public void Settings_default_variant_is_ZeroShot()
        => Assert.Equal(PromptVariant.ZeroShot, new MigrationSettings(3).Variant);
}
