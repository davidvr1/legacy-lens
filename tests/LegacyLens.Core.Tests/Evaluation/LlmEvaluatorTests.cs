using LegacyLens.Core.Evaluation;
using LegacyLens.Core.Llm;
using LegacyLens.Core.Migration;
using LegacyLens.Core.Model;
using LegacyLens.Core.Rules;
using LegacyLens.Core.Summary;
using LegacyLens.Core.Tests.Migration;
using LegacyLens.Core.Tests.Summary;

namespace LegacyLens.Core.Tests.Evaluation;

public sealed class LlmEvaluatorTests
{
    private sealed class FakeRuleEngine(params Finding[] findings) : IRuleEngine
    {
        public int Runs { get; private set; }

        public IReadOnlyList<Finding> Run(SourceFile file)
        {
            Runs++;
            return findings;
        }
    }

    private static readonly LlmEvalCase Case = new(
        "case-1",
        MigrationTestData.File("<%\nid = Request.QueryString(\"orderId\")\n%>"),
        [new ExpectedFact(SummaryCategory.Inputs, ["orderId"])]);

    private static LlmEvalOptions Options(bool summary = true, bool migration = true, PromptVariant variant = PromptVariant.ZeroShot, int topK = 3)
        => new(summary, migration, variant, topK);

    private static CodeSummary GoodSummary() => new("Shows orders.", ["QueryString: orderId"], [], [], [], []);

    private static LlmEvaluator Evaluator(FakeRetriever? retriever = null, FakeCodeValidator? validator = null, FakeRuleEngine? rules = null)
        => new(
            rules ?? new FakeRuleEngine(MigrationTestData.Finding()),
            retriever ?? new FakeRetriever([MigrationTestData.ChunkA, MigrationTestData.ChunkB]),
            validator ?? new FakeCodeValidator(MigrationTestData.Ok()));

    [Fact]
    public async Task Summary_only_scores_summary_and_skips_migration()
    {
        var llm = new FakeLlmClient().Enqueue(GoodSummary());
        var retriever = new FakeRetriever([MigrationTestData.ChunkA]);

        var result = await Evaluator(retriever).RunCaseAsync(llm, Case, Options(migration: false), CancellationToken.None);

        Assert.Equal("case-1", result.CaseId);
        Assert.NotNull(result.Summary);
        Assert.Equal(1.0, result.Summary!.FactRecall);
        Assert.Null(result.SummaryError);
        Assert.Null(result.Migration);
        Assert.Null(result.MigrationError);
        Assert.Equal(TimeSpan.Zero, result.MigrationDuration);
        Assert.Single(llm.Prompts);
        Assert.Empty(retriever.Calls);
    }

    [Fact]
    public async Task Migration_only_scores_migration_and_skips_summary()
    {
        var llm = new FakeLlmClient().Enqueue(MigrationTestData.Reply("class A {}", "sql-injection"));

        var result = await Evaluator().RunCaseAsync(llm, Case, Options(summary: false), CancellationToken.None);

        Assert.Null(result.Summary);
        Assert.Null(result.SummaryError);
        Assert.Equal(TimeSpan.Zero, result.SummaryDuration);
        Assert.NotNull(result.Migration);
        Assert.Null(result.MigrationError);
        Assert.True(result.Migration!.CompiledFirstAttempt);
        Assert.True(result.Migration.CompiledAfterRepair);
        Assert.True(result.Migration.SourcesValid);
        Assert.Single(llm.Prompts);
    }

    [Fact]
    public async Task Both_tasks_run_summary_first_then_migration()
    {
        var llm = new FakeLlmClient().Enqueue(GoodSummary()).Enqueue(MigrationTestData.Reply("class A {}", "xss-output"));

        var result = await Evaluator().RunCaseAsync(llm, Case, Options(), CancellationToken.None);

        Assert.NotNull(result.Summary);
        Assert.NotNull(result.Migration);
        Assert.Equal(2, llm.Prompts.Count);
        Assert.Equal(["xss-output"], result.Migration!.CitedIds);
    }

    [Fact]
    public async Task Keeps_the_model_output_behind_the_scores()
    {
        var summary = GoodSummary();
        var llm = new FakeLlmClient().Enqueue(summary).Enqueue(MigrationTestData.Reply("class A {}", "xss-output"));

        var result = await Evaluator().RunCaseAsync(llm, Case, Options(), CancellationToken.None);

        Assert.Equal(summary, result.StatedSummary);
        Assert.Equal("class A {}", result.Proposal!.Code);
    }

    [Fact]
    public async Task Tasks_not_run_keep_no_model_output()
    {
        var result = await Evaluator().RunCaseAsync(new FakeLlmClient(), Case, Options(summary: false, migration: false), CancellationToken.None);

        Assert.Null(result.StatedSummary);
        Assert.Null(result.Proposal);
    }

    [Fact]
    public async Task Neither_task_runs_nothing()
    {
        var llm = new FakeLlmClient();
        var result = await Evaluator().RunCaseAsync(llm, Case, Options(false, false), CancellationToken.None);
        Assert.Null(result.Summary);
        Assert.Null(result.Migration);
        Assert.Empty(llm.Prompts);
    }

    [Fact]
    public async Task Rule_engine_runs_on_case_file()
    {
        var rules = new FakeRuleEngine(MigrationTestData.Finding("MY-FINDING-TITLE"));
        var llm = new FakeLlmClient().Enqueue(GoodSummary());

        await Evaluator(rules: rules).RunCaseAsync(llm, Case, Options(migration: false), CancellationToken.None);

        Assert.Equal(1, rules.Runs);
        Assert.Contains("MY-FINDING-TITLE", llm.Prompts[0]);
    }

    [Fact]
    public async Task Summary_error_is_reported_not_thrown()
    {
        var llm = new FakeLlmClient().Enqueue(new LlmException("down"));

        var result = await Evaluator().RunCaseAsync(llm, Case, Options(migration: false), CancellationToken.None);

        Assert.Null(result.Summary);
        Assert.Equal(SummaryService.UnavailableError, result.SummaryError);
    }

    [Fact]
    public async Task Summary_invalid_twice_is_an_error()
    {
        var llm = new FakeLlmClient()
            .Enqueue(new LlmInvalidResponseException("x"))
            .Enqueue(new LlmInvalidResponseException("y"));

        var result = await Evaluator().RunCaseAsync(llm, Case, Options(migration: false), CancellationToken.None);

        Assert.Equal(SummaryService.InvalidResponseError, result.SummaryError);
    }

    [Fact]
    public async Task Summary_error_does_not_stop_migration()
    {
        var llm = new FakeLlmClient().Enqueue(new LlmException("down")).Enqueue(MigrationTestData.Reply("class A {}", "sql-injection"));

        var result = await Evaluator().RunCaseAsync(llm, Case, Options(), CancellationToken.None);

        Assert.NotNull(result.SummaryError);
        Assert.NotNull(result.Migration);
    }

    [Fact]
    public async Task Migration_error_is_reported_not_thrown()
    {
        var llm = new FakeLlmClient().Enqueue(new LlmException("down"));

        var result = await Evaluator().RunCaseAsync(llm, Case, Options(summary: false), CancellationToken.None);

        Assert.Null(result.Migration);
        Assert.Equal(MigrationService.UnavailableError, result.MigrationError);
    }

    [Fact]
    public async Task Prompt_ids_come_from_first_prompt_and_cited_ids_from_reply()
    {
        var llm = new FakeLlmClient().Enqueue(MigrationTestData.Reply("class A {}", "sql-injection", "bogus"));

        var result = await Evaluator().RunCaseAsync(llm, Case, Options(summary: false), CancellationToken.None);

        Assert.Equal(["sql-injection", "xss-output"], result.Migration!.PromptIds);
        Assert.Equal(["sql-injection", "bogus"], result.Migration.CitedIds);
        Assert.False(result.Migration.SourcesValid);
    }

    [Fact]
    public async Task Cited_ids_come_from_last_valid_reply_after_repair()
    {
        var llm = new FakeLlmClient()
            .Enqueue(MigrationTestData.Reply("bad code", "sql-injection"))
            .Enqueue(MigrationTestData.Reply("good code", "xss-output"));
        var validator = new FakeCodeValidator(MigrationTestData.Fail(), MigrationTestData.Ok());

        var result = await Evaluator(validator: validator).RunCaseAsync(llm, Case, Options(summary: false), CancellationToken.None);

        Assert.Equal(["xss-output"], result.Migration!.CitedIds);
        Assert.False(result.Migration.CompiledFirstAttempt);
        Assert.True(result.Migration.CompiledAfterRepair);
        Assert.Equal(["sql-injection", "xss-output"], result.Migration.PromptIds);
    }

    [Fact]
    public async Task Invalid_repair_reply_is_ignored_for_cited_ids()
    {
        var invalidRepair = new MigrationReply("", ["n"], ["xss-output"]);
        var llm = new FakeLlmClient()
            .Enqueue(MigrationTestData.Reply("bad code", "sql-injection"))
            .Enqueue(invalidRepair);
        var validator = new FakeCodeValidator(MigrationTestData.Fail());

        var result = await Evaluator(validator: validator).RunCaseAsync(llm, Case, Options(summary: false), CancellationToken.None);

        Assert.Equal(["sql-injection"], result.Migration!.CitedIds);
        Assert.False(result.Migration.CompiledAfterRepair);
    }

    [Fact]
    public async Task Prompt_ids_come_from_first_prompt_not_retry_prompt()
    {
        var llm = new FakeLlmClient()
            .Enqueue(new LlmInvalidResponseException("bad json"))
            .Enqueue(MigrationTestData.Reply("class A {}", "sql-injection"));

        var result = await Evaluator().RunCaseAsync(llm, Case, Options(summary: false), CancellationToken.None);

        Assert.Equal(["sql-injection", "xss-output"], result.Migration!.PromptIds);
        Assert.Equal(["sql-injection"], result.Migration.CitedIds);
    }

    [Fact]
    public async Task Variant_and_topk_options_are_used()
    {
        var retriever = new FakeRetriever([MigrationTestData.ChunkA]);
        var llm = new FakeLlmClient().Enqueue(GoodSummary()).Enqueue(MigrationTestData.Reply("class A {}", "sql-injection"));

        await Evaluator(retriever).RunCaseAsync(llm, Case, Options(variant: PromptVariant.FewShot, topK: 5), CancellationToken.None);

        Assert.Equal(5, Assert.Single(retriever.Calls).TopK);
        Assert.Contains(SummaryPromptBuilder.FewShotExampleHeader, llm.Prompts[0]);
        Assert.Contains(MigrationPromptBuilder.FewShotExampleHeader, llm.Prompts[1]);
    }

    [Fact]
    public async Task Summary_hallucination_is_scored_against_case_code()
    {
        var llm = new FakeLlmClient().Enqueue(new CodeSummary("p", ["QueryString: orderId"], [], [], [], ["PaymentGatewayX"]));

        var result = await Evaluator().RunCaseAsync(llm, Case, Options(migration: false), CancellationToken.None);

        Assert.Equal(1, result.Summary!.HallucinatedItems);
        Assert.Equal(0.5, result.Summary.HallucinationRate);
    }

    [Fact]
    public async Task Trace_of_a_first_try_compile_has_no_repair()
    {
        var llm = new FakeLlmClient().Enqueue(MigrationTestData.Reply("class Good {}", "sql-injection"));

        var result = await Evaluator().RunCaseAsync(llm, Case, Options(summary: false), CancellationToken.None);

        var trace = result.MigrationTrace!;
        Assert.Equal("class Good {}", trace.FirstCode);
        Assert.Empty(trace.FirstDiagnostics);
        Assert.False(trace.FirstTruncated);
        Assert.False(trace.RepairAttempted);
        Assert.Null(trace.RepairError);
    }

    [Fact]
    public async Task Trace_keeps_the_first_attempt_when_the_repair_is_used()
    {
        var llm = new FakeLlmClient()
            .Enqueue(MigrationTestData.Reply("class Bad { void M() { x = Request.Form[", "sql-injection"))
            .Enqueue(MigrationTestData.Reply("class Good {}", "sql-injection"));
        var validator = new FakeCodeValidator(MigrationTestData.Fail(id: "CS1513", message: "} expected"), MigrationTestData.Ok());

        var result = await Evaluator(validator: validator).RunCaseAsync(llm, Case, Options(summary: false), CancellationToken.None);

        var trace = result.MigrationTrace!;
        Assert.Equal("class Bad { void M() { x = Request.Form[", trace.FirstCode);
        Assert.Equal("CS1513", Assert.Single(trace.FirstDiagnostics).Id);
        Assert.True(trace.FirstTruncated);
        Assert.True(trace.RepairAttempted);
        Assert.Null(trace.RepairError);
        Assert.Equal("class Good {}", result.Proposal!.Code);
    }

    [Fact]
    public async Task Trace_gives_the_validation_error_of_an_invalid_repair_reply()
    {
        var llm = new FakeLlmClient()
            .Enqueue(MigrationTestData.Reply("bad code", "sql-injection"))
            .Enqueue(new MigrationReply("", ["n"], ["sql-injection"]));
        var validator = new FakeCodeValidator(MigrationTestData.Fail());

        var result = await Evaluator(validator: validator).RunCaseAsync(llm, Case, Options(summary: false), CancellationToken.None);

        var trace = result.MigrationTrace!;
        Assert.True(trace.RepairAttempted);
        Assert.Equal(MigrationReplyValidator.Validate(new MigrationReply("", ["n"], ["sql-injection"])), trace.RepairError);
        Assert.NotNull(trace.RepairError);
    }

    [Fact]
    public async Task Trace_gives_the_message_of_a_repair_call_that_threw()
    {
        var llm = new FakeLlmClient()
            .Enqueue(MigrationTestData.Reply("bad code", "sql-injection"))
            .Enqueue(new LlmInvalidResponseException("not JSON"));
        var validator = new FakeCodeValidator(MigrationTestData.Fail());

        var result = await Evaluator(validator: validator).RunCaseAsync(llm, Case, Options(summary: false), CancellationToken.None);

        Assert.Equal("not JSON", result.MigrationTrace!.RepairError);
        Assert.False(result.Proposal!.Repaired);
    }

    [Fact]
    public async Task No_trace_when_no_reply_was_compiled_or_migration_not_run()
    {
        var down = await Evaluator().RunCaseAsync(new FakeLlmClient().Enqueue(new LlmException("down")), Case, Options(summary: false), CancellationToken.None);
        var skipped = await Evaluator().RunCaseAsync(new FakeLlmClient().Enqueue(GoodSummary()), Case, Options(migration: false), CancellationToken.None);

        Assert.Null(down.MigrationTrace);
        Assert.Null(skipped.MigrationTrace);
    }
}
