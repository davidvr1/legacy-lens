using LegacyLens.Core.Migration;
using LegacyLens.Core.Validation;
using static LegacyLens.Core.Tests.Migration.MigrationTestData;

namespace LegacyLens.Core.Tests.Migration;

public sealed class MigrationPromptBuilderTests
{
    [Fact]
    public void Build_includes_line_numbered_code_and_findings()
    {
        var prompt = MigrationPromptBuilder.Build(File("a\nb\nc"), [Finding("My title", 2)], []);

        Assert.Contains("1: a\n2: b\n3: c", prompt);
        Assert.Contains("SQL001", prompt);
        Assert.Contains("line 2: My title", prompt);
        Assert.Contains("page.asp", prompt);
        Assert.Contains(MigrationSchema.Json, prompt);
    }

    [Fact]
    public void Build_states_the_requirements()
    {
        var prompt = MigrationPromptBuilder.Build(File(), [], []);

        Assert.Contains("DbDataSource", prompt);
        Assert.Contains("IConfiguration", prompt);
        Assert.Contains("CancellationToken", prompt);
        Assert.Contains("No NuGet packages", prompt);
    }

    [Fact]
    public void Build_lists_every_chunk_with_header_prefix_title_and_content()
    {
        var prompt = MigrationPromptBuilder.Build(File(), [], [ChunkA, ChunkB]);

        Assert.Contains(MigrationPromptBuilder.SourceHeaderPrefix + "sql-injection", prompt);
        Assert.Contains(MigrationPromptBuilder.SourceHeaderPrefix + "xss-output", prompt);
        Assert.Contains("Title: SQL Injection", prompt);
        Assert.Contains("Use parameters.", prompt);
        Assert.Contains("Encode output.", prompt);
    }

    [Fact]
    public void Build_without_chunks_and_findings_says_none_and_has_no_source_headers()
    {
        var prompt = MigrationPromptBuilder.Build(File(), [], []);

        Assert.Contains("Knowledge notes:\n- none", prompt);
        Assert.Contains("must all be fixed):\n- none", prompt);
        Assert.DoesNotContain(MigrationPromptBuilder.SourceHeaderPrefix, prompt);
    }

    [Fact]
    public void ParseSourceIds_round_trips_in_order()
    {
        var prompt = MigrationPromptBuilder.Build(File(), [], [ChunkB, ChunkA]);

        Assert.Equal(["xss-output", "sql-injection"], MigrationPromptBuilder.ParseSourceIds(prompt));
    }

    [Fact]
    public void ParseSourceIds_returns_empty_when_none()
    {
        Assert.Empty(MigrationPromptBuilder.ParseSourceIds(MigrationPromptBuilder.Build(File(), [], [])));
        Assert.Empty(MigrationPromptBuilder.ParseSourceIds(string.Empty));
    }

    [Fact]
    public void ParseSourceIds_ignores_the_prefix_when_not_at_line_start_and_trims()
    {
        var ids = MigrationPromptBuilder.ParseSourceIds(
            "text ### Source id: nope\n### Source id: a-b  \r\nother\n### Source id: c");

        Assert.Equal(["a-b", "c"], ids);
    }

    [Fact]
    public void BuildRetry_contains_original_prompt_and_error()
    {
        var retry = MigrationPromptBuilder.BuildRetry("ORIGINAL", "code is required.");

        Assert.Contains("ORIGINAL", retry);
        Assert.Contains("code is required.", retry);
    }

    [Fact]
    public void BuildRepair_contains_prompt_previous_code_and_every_diagnostic_line()
    {
        var repair = MigrationPromptBuilder.BuildRepair(
            "ORIGINAL",
            "class Broken {}",
            [new CodeDiagnostic("CS0103", "name missing", 4), new CodeDiagnostic("CS1002", "; expected", 7)]);

        Assert.Contains("ORIGINAL", repair);
        Assert.Contains("class Broken {}", repair);
        Assert.Contains("did not compile", repair);
        Assert.Contains("- line 4: CS0103 name missing", repair);
        Assert.Contains("- line 7: CS1002 ; expected", repair);
    }
}
