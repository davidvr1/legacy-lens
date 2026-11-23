using LegacyLens.Core.Evaluation;
using LegacyLens.Core.Model;

namespace LegacyLens.Core.Tests.Evaluation;

public sealed class MigrationScorerTests
{
    private static MigrationProposal Proposal(bool compiles, bool repaired)
        => new("class A {}", [], [], compiles, [], repaired);

    [Fact]
    public void Compiled_first_time()
    {
        var s = MigrationScorer.Score(Proposal(true, false), ["a"], ["a"]);
        Assert.True(s.CompiledFirstAttempt);
        Assert.True(s.CompiledAfterRepair);
    }

    [Fact]
    public void Compiled_only_after_repair()
    {
        var s = MigrationScorer.Score(Proposal(true, true), ["a"], ["a"]);
        Assert.False(s.CompiledFirstAttempt);
        Assert.True(s.CompiledAfterRepair);
    }

    [Fact]
    public void Never_compiled_after_repair_attempt()
    {
        var s = MigrationScorer.Score(Proposal(false, true), ["a"], ["a"]);
        Assert.False(s.CompiledFirstAttempt);
        Assert.False(s.CompiledAfterRepair);
    }

    [Fact]
    public void Never_compiled_without_repair()
    {
        var s = MigrationScorer.Score(Proposal(false, false), ["a"], ["a"]);
        Assert.False(s.CompiledFirstAttempt);
        Assert.False(s.CompiledAfterRepair);
    }

    [Fact]
    public void Sources_valid_when_all_cited_are_in_prompt()
        => Assert.True(MigrationScorer.Score(Proposal(true, false), ["a", "b"], ["a", "b", "c"]).SourcesValid);

    [Fact]
    public void Sources_invalid_when_one_cited_is_not_in_prompt()
        => Assert.False(MigrationScorer.Score(Proposal(true, false), ["a", "zzz"], ["a", "b"]).SourcesValid);

    [Fact]
    public void Sources_invalid_when_none_cited()
        => Assert.False(MigrationScorer.Score(Proposal(true, false), [], ["a", "b"]).SourcesValid);

    [Fact]
    public void Sources_invalid_when_prompt_has_no_ids()
        => Assert.False(MigrationScorer.Score(Proposal(true, false), ["a"], []).SourcesValid);

    [Fact]
    public void Sources_compare_case_insensitively()
        => Assert.True(MigrationScorer.Score(Proposal(true, false), ["SQL-Injection"], ["sql-injection"]).SourcesValid);

    [Fact]
    public void Ids_are_passed_through()
    {
        var s = MigrationScorer.Score(Proposal(true, false), ["a"], ["a", "b"]);
        Assert.Equal(["a"], s.CitedIds);
        Assert.Equal(["a", "b"], s.PromptIds);
    }
}
