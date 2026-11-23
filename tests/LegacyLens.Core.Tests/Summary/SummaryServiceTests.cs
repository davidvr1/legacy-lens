using LegacyLens.Core.Llm;
using LegacyLens.Core.Model;
using LegacyLens.Core.Summary;

namespace LegacyLens.Core.Tests.Summary;

public sealed class SummaryServiceTests
{
    private static Task<SummaryResult> Run(FakeLlmClient fake, CancellationToken ct = default)
        => new SummaryService(fake, new SummarySettings(PromptVariant.ZeroShot)).SummarizeAsync(SummaryTestData.File(), [], ct);

    [Fact]
    public async Task Valid_reply_first_try()
    {
        var fake = new FakeLlmClient().Enqueue(SummaryTestData.Valid());
        var result = await Run(fake);
        Assert.NotNull(result.Summary);
        Assert.Null(result.Error);
        Assert.Single(fake.Prompts);
    }

    [Fact]
    public async Task Invalid_json_then_valid_retries_with_error_in_prompt()
    {
        var fake = new FakeLlmClient()
            .Enqueue(new LlmInvalidResponseException("bad json at position 7"))
            .Enqueue(SummaryTestData.Valid());
        var result = await Run(fake);
        Assert.NotNull(result.Summary);
        Assert.Null(result.Error);
        Assert.Equal(2, fake.Prompts.Count);
        Assert.Contains("bad json at position 7", fake.Prompts[1]);
        Assert.Contains(fake.Prompts[0], fake.Prompts[1]);
    }

    public static TheoryData<CodeSummary, string> InvalidReplies => new()
    {
        { new CodeSummary("", [], [], [], [], []), "purpose is required." },
        { new CodeSummary("p", null!, [], [], [], []), "inputs must be an array of non-empty strings." },
        { new CodeSummary("p", [], [], [], [" "], []), "includes must be an array of non-empty strings." },
    };

    [Theory]
    [MemberData(nameof(InvalidReplies))]
    public async Task Validator_failure_then_valid_retries(CodeSummary bad, string expectedError)
    {
        var fake = new FakeLlmClient().Enqueue(bad).Enqueue(SummaryTestData.Valid());
        var result = await Run(fake);
        Assert.NotNull(result.Summary);
        Assert.Equal(2, fake.Prompts.Count);
        Assert.Contains(expectedError, fake.Prompts[1]);
    }

    [Fact]
    public async Task Invalid_twice_gives_InvalidResponseError_after_exactly_two_calls()
    {
        var fake = new FakeLlmClient()
            .Enqueue(new LlmInvalidResponseException("x"))
            .Enqueue(new CodeSummary("", [], [], [], [], []));
        var result = await Run(fake);
        Assert.Null(result.Summary);
        Assert.Equal(SummaryService.InvalidResponseError, result.Error);
        Assert.Equal(2, fake.Prompts.Count);
    }

    [Fact]
    public async Task LlmException_on_first_call_is_unavailable_without_retry()
    {
        var fake = new FakeLlmClient().Enqueue(new LlmException("down"));
        var result = await Run(fake);
        Assert.Null(result.Summary);
        Assert.Equal(SummaryService.UnavailableError, result.Error);
        Assert.Single(fake.Prompts);
    }

    [Fact]
    public async Task LlmException_on_retry_is_unavailable()
    {
        var fake = new FakeLlmClient()
            .Enqueue(new LlmInvalidResponseException("x"))
            .Enqueue(new LlmException("down"));
        var result = await Run(fake);
        Assert.Null(result.Summary);
        Assert.Equal(SummaryService.UnavailableError, result.Error);
        Assert.Equal(2, fake.Prompts.Count);
    }

    [Fact]
    public async Task Cancelled_token_propagates()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var fake = new FakeLlmClient().Enqueue(SummaryTestData.Valid());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Run(fake, cts.Token));
    }

    [Fact]
    public async Task OperationCanceledException_from_client_propagates()
    {
        var fake = new FakeLlmClient().Enqueue(new OperationCanceledException());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Run(fake));
    }
}
