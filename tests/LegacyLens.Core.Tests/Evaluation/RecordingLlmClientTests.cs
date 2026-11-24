using LegacyLens.Core.Evaluation;
using LegacyLens.Core.Llm;
using LegacyLens.Core.Model;
using LegacyLens.Core.Tests.Summary;

namespace LegacyLens.Core.Tests.Evaluation;

public sealed class RecordingLlmClientTests
{
    [Fact]
    public void Starts_empty() => Assert.Empty(new RecordingLlmClient(new FakeLlmClient()).Calls);

    [Fact]
    public async Task Records_prompt_and_reply_and_returns_reply()
    {
        var reply = SummaryTestData.Valid();
        var recorder = new RecordingLlmClient(new FakeLlmClient().Enqueue(reply));

        var result = await recorder.CompleteJsonAsync<CodeSummary>("my prompt", "{}", CancellationToken.None);

        Assert.Same(reply, result);
        var call = Assert.Single(recorder.Calls);
        Assert.Equal("my prompt", call.Prompt);
        Assert.Same(reply, call.Reply);
    }

    [Fact]
    public async Task Records_calls_in_order()
    {
        var recorder = new RecordingLlmClient(new FakeLlmClient().Enqueue(SummaryTestData.Valid()).Enqueue(SummaryTestData.Valid()));
        await recorder.CompleteJsonAsync<CodeSummary>("first", "{}", CancellationToken.None);
        await recorder.CompleteJsonAsync<CodeSummary>("second", "{}", CancellationToken.None);
        Assert.Equal(["first", "second"], recorder.Calls.Select(c => c.Prompt));
    }

    [Fact]
    public async Task Exception_is_recorded_as_null_reply_and_rethrown_unchanged()
    {
        var boom = new LlmInvalidResponseException("bad");
        var recorder = new RecordingLlmClient(new FakeLlmClient().Enqueue(boom));

        var ex = await Assert.ThrowsAsync<LlmInvalidResponseException>(
            () => recorder.CompleteJsonAsync<CodeSummary>("p", "{}", CancellationToken.None));

        Assert.Same(boom, ex);
        var call = Assert.Single(recorder.Calls);
        Assert.Equal("p", call.Prompt);
        Assert.Null(call.Reply);
        Assert.Equal("bad", call.Error);
    }
}
