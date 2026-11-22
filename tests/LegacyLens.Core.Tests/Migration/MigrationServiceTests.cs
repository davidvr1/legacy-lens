using LegacyLens.Core.Llm;
using LegacyLens.Core.Migration;
using LegacyLens.Core.Model;
using LegacyLens.Core.Tests.Summary;
using static LegacyLens.Core.Tests.Migration.MigrationTestData;

namespace LegacyLens.Core.Tests.Migration;

public sealed class MigrationServiceTests
{
    private static MigrationService Create(FakeLlmClient llm, FakeRetriever retriever, FakeCodeValidator validator, int topK = 4)
        => new(llm, retriever, validator, new MigrationSettings(topK));

    private static Task<MigrationResult> Run(MigrationService s, CancellationToken ct = default)
        => s.ProposeAsync(File(), [Finding()], ct);

    [Fact]
    public async Task Happy_path_returns_proposal_with_mapped_sources()
    {
        var llm = new FakeLlmClient().Enqueue(Reply("class A {}", "sql-injection"));
        var service = Create(llm, new FakeRetriever([ChunkA, ChunkB]), new FakeCodeValidator(Ok()));

        var result = await Run(service);

        Assert.Null(result.Error);
        var p = result.Proposal!;
        Assert.Equal("class A {}", p.Code);
        Assert.Equal(["note"], p.Notes);
        Assert.True(p.CompilesOk);
        Assert.False(p.Repaired);
        Assert.Empty(p.Diagnostics);
        Assert.Equal(new MigrationSource("sql-injection", "SQL Injection"), Assert.Single(p.Sources));
        Assert.Single(llm.Prompts);
    }

    [Fact]
    public async Task Topk_from_settings_and_query_with_code_and_distinct_titles_go_to_retriever()
    {
        var retriever = new FakeRetriever([ChunkA]);
        var llm = new FakeLlmClient().Enqueue(Reply());
        var service = Create(llm, retriever, new FakeCodeValidator(Ok()), topK: 7);

        await service.ProposeAsync(File("CODE-HERE"), [Finding("T1"), Finding("T1"), Finding("T2")], CancellationToken.None);

        var call = Assert.Single(retriever.Calls);
        Assert.Equal(7, call.TopK);
        Assert.Equal("CODE-HERE\nT1\nT2", call.Query);
    }

    [Fact]
    public async Task Invalid_json_then_valid_retries_once_with_error_in_prompt()
    {
        var llm = new FakeLlmClient()
            .Enqueue(new LlmInvalidResponseException("bad-json-xyz"))
            .Enqueue(Reply());
        var service = Create(llm, new FakeRetriever([]), new FakeCodeValidator(Ok()));

        var result = await Run(service);

        Assert.NotNull(result.Proposal);
        Assert.Equal(2, llm.Prompts.Count);
        Assert.Contains("bad-json-xyz", llm.Prompts[1]);
        Assert.StartsWith(llm.Prompts[0], llm.Prompts[1]);
    }

    [Fact]
    public async Task Validator_error_triggers_retry_with_error_in_prompt()
    {
        var llm = new FakeLlmClient()
            .Enqueue(new MigrationReply("  ", ["n"], []))
            .Enqueue(Reply());
        var service = Create(llm, new FakeRetriever([]), new FakeCodeValidator(Ok()));

        var result = await Run(service);

        Assert.NotNull(result.Proposal);
        Assert.Contains("code is required.", llm.Prompts[1]);
    }

    [Fact]
    public async Task Invalid_twice_returns_invalid_response_error_and_makes_two_calls()
    {
        var llm = new FakeLlmClient()
            .Enqueue(new LlmInvalidResponseException("a"))
            .Enqueue(new LlmInvalidResponseException("b"));
        var validator = new FakeCodeValidator(Ok());
        var service = Create(llm, new FakeRetriever([]), validator);

        var result = await Run(service);

        Assert.Null(result.Proposal);
        Assert.Equal(MigrationService.InvalidResponseError, result.Error);
        Assert.Equal(2, llm.Prompts.Count);
        Assert.Empty(validator.Codes);
    }

    [Fact]
    public async Task Llm_exception_gives_unavailable_error()
    {
        var llm = new FakeLlmClient().Enqueue(new LlmException("down"));
        var service = Create(llm, new FakeRetriever([]), new FakeCodeValidator(Ok()));

        var result = await Run(service);

        Assert.Null(result.Proposal);
        Assert.Equal(MigrationService.UnavailableError, result.Error);
    }

    [Fact]
    public async Task Retriever_llm_exception_gives_unavailable_error()
    {
        var llm = new FakeLlmClient();
        var service = Create(llm, new FakeRetriever([], new LlmException("embed down")), new FakeCodeValidator(Ok()));

        var result = await Run(service);

        Assert.Equal(MigrationService.UnavailableError, result.Error);
        Assert.Empty(llm.Prompts);
    }

    [Fact]
    public async Task Llm_exception_on_retry_gives_unavailable_error()
    {
        var llm = new FakeLlmClient()
            .Enqueue(new LlmInvalidResponseException("a"))
            .Enqueue(new LlmException("down"));
        var service = Create(llm, new FakeRetriever([]), new FakeCodeValidator(Ok()));

        var result = await Run(service);

        Assert.Equal(MigrationService.UnavailableError, result.Error);
    }

    [Fact]
    public async Task Compile_failure_makes_one_repair_call_with_diagnostics_and_uses_final_result()
    {
        var llm = new FakeLlmClient()
            .Enqueue(Reply("broken code"))
            .Enqueue(Reply("fixed code"));
        var validator = new FakeCodeValidator(Fail(line: 9, id: "CS1002", message: "; expected"), Ok());
        var service = Create(llm, new FakeRetriever([]), validator);

        var result = await Run(service);

        var p = result.Proposal!;
        Assert.Equal(2, llm.Prompts.Count);
        Assert.Contains("line 9: CS1002 ; expected", llm.Prompts[1]);
        Assert.Contains("broken code", llm.Prompts[1]);
        Assert.True(p.Repaired);
        Assert.True(p.CompilesOk);
        Assert.Equal("fixed code", p.Code);
        Assert.Empty(p.Diagnostics);
        Assert.Equal(["broken code", "fixed code"], validator.Codes);
    }

    [Fact]
    public async Task Repair_that_still_fails_uses_repair_code_and_its_diagnostics_without_second_repair()
    {
        var llm = new FakeLlmClient()
            .Enqueue(Reply("first"))
            .Enqueue(Reply("second"));
        var validator = new FakeCodeValidator(Fail(line: 1, id: "CS0001"), Fail(line: 5, id: "CS0002"));
        var service = Create(llm, new FakeRetriever([]), validator);

        var result = await Run(service);

        var p = result.Proposal!;
        Assert.Equal(2, llm.Prompts.Count);
        Assert.True(p.Repaired);
        Assert.False(p.CompilesOk);
        Assert.Equal("second", p.Code);
        Assert.Equal("CS0002", Assert.Single(p.Diagnostics).Id);
    }

    [Fact]
    public async Task Invalid_repair_reply_keeps_first_reply_and_diagnostics()
    {
        var llm = new FakeLlmClient()
            .Enqueue(Reply("first"))
            .Enqueue(new LlmInvalidResponseException("garbage"));
        var validator = new FakeCodeValidator(Fail(line: 4, id: "CS0103"));
        var service = Create(llm, new FakeRetriever([]), validator);

        var result = await Run(service);

        var p = result.Proposal!;
        Assert.Equal("first", p.Code);
        Assert.False(p.Repaired);
        Assert.False(p.CompilesOk);
        Assert.Equal("CS0103", Assert.Single(p.Diagnostics).Id);
        Assert.Equal(2, llm.Prompts.Count);
    }

    [Fact]
    public async Task Repair_reply_failing_the_validator_keeps_first_reply()
    {
        var llm = new FakeLlmClient()
            .Enqueue(Reply("first"))
            .Enqueue(new MigrationReply("", ["n"], []));
        var service = Create(llm, new FakeRetriever([]), new FakeCodeValidator(Fail()));

        var result = await Run(service);

        Assert.Equal("first", result.Proposal!.Code);
        Assert.False(result.Proposal.Repaired);
        Assert.Equal(2, llm.Prompts.Count);
    }

    [Fact]
    public async Task Llm_exception_during_repair_gives_unavailable_error()
    {
        var llm = new FakeLlmClient().Enqueue(Reply()).Enqueue(new LlmException("down"));
        var service = Create(llm, new FakeRetriever([]), new FakeCodeValidator(Fail()));

        var result = await Run(service);

        Assert.Equal(MigrationService.UnavailableError, result.Error);
    }

    [Fact]
    public async Task Sources_unknown_duplicate_and_differently_cased_ids_are_filtered()
    {
        var llm = new FakeLlmClient().Enqueue(Reply(
            "c", "XSS-OUTPUT", "not-in-prompt", "sql-injection", "xss-output", "SQL-Injection"));
        var service = Create(llm, new FakeRetriever([ChunkA, ChunkB]), new FakeCodeValidator(Ok()));

        var result = await Run(service);

        Assert.Equal(
            [new MigrationSource("xss-output", "XSS Output"), new MigrationSource("sql-injection", "SQL Injection")],
            result.Proposal!.Sources);
    }

    [Fact]
    public async Task Sources_are_empty_when_no_chunks_were_retrieved()
    {
        var llm = new FakeLlmClient().Enqueue(Reply("c", "sql-injection"));
        var service = Create(llm, new FakeRetriever([]), new FakeCodeValidator(Ok()));

        var result = await Run(service);

        Assert.Empty(result.Proposal!.Sources);
    }

    [Fact]
    public async Task Cancellation_propagates()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var service = Create(new FakeLlmClient().Enqueue(Reply()), new FakeRetriever([]), new FakeCodeValidator(Ok()));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Run(service, cts.Token));
    }

    [Fact]
    public async Task Cancellation_from_llm_is_not_turned_into_an_error()
    {
        var llm = new FakeLlmClient().Enqueue(new OperationCanceledException());
        var service = Create(llm, new FakeRetriever([]), new FakeCodeValidator(Ok()));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Run(service));
    }
}
