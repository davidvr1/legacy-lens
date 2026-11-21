using LegacyLens.Core.Llm;
using LegacyLens.Core.Model;
using LegacyLens.Infrastructure.Llm;

namespace LegacyLens.Api.Tests;

public sealed class DemoLlmClientTests
{
    [Fact]
    public async Task CodeSummary_returns_DemoSummary()
    {
        var result = await new DemoLlmClient().CompleteJsonAsync<CodeSummary>("p", "{}", CancellationToken.None);
        Assert.Same(DemoLlmClient.DemoSummary, result);
    }

    [Fact]
    public async Task Other_type_throws_LlmException()
    {
        await Assert.ThrowsAsync<LlmException>(() => new DemoLlmClient().CompleteJsonAsync<string>("p", "{}", CancellationToken.None));
    }

    [Fact]
    public async Task Cancelled_token_throws_OperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new DemoLlmClient().CompleteJsonAsync<CodeSummary>("p", "{}", cts.Token));
    }
}
