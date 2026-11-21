using System.Net;
using System.Text;
using System.Text.Json;
using LegacyLens.Core.Llm;
using LegacyLens.Core.Model;
using LegacyLens.Core.Summary;
using LegacyLens.Infrastructure.Llm;
using Microsoft.Extensions.Options;

namespace LegacyLens.Api.Tests;

public sealed class OllamaLlmClientTests
{
    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK)
        => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static string Envelope(string response) => JsonSerializer.Serialize(new { response, done = true });

    private static OllamaLlmClient Client(FakeHandler handler) => new(
        new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434/") },
        Options.Create(new LlmOptions { Model = "test-model" }));

    private const string ValidReply =
        """{"purpose":"p","inputs":["a"],"outputs":[],"databaseAccess":[],"includes":["i"],"comObjects":[]}""";

    [Fact]
    public async Task Sends_expected_request_and_deserializes_reply()
    {
        var handler = new FakeHandler((_, _, _) => Task.FromResult(Json(Envelope(ValidReply))));
        var summary = await Client(handler).CompleteJsonAsync<CodeSummary>("my prompt", CodeSummarySchema.Json, CancellationToken.None);

        Assert.Equal("p", summary.Purpose);
        Assert.Equal(["a"], summary.Inputs);
        Assert.Equal(["i"], summary.Includes);
        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal("http://localhost:11434/api/generate", handler.Request.RequestUri!.ToString());

        using var doc = JsonDocument.Parse(handler.Body!);
        var root = doc.RootElement;
        Assert.Equal("test-model", root.GetProperty("model").GetString());
        Assert.Equal("my prompt", root.GetProperty("prompt").GetString());
        Assert.False(root.GetProperty("stream").GetBoolean());
        Assert.Equal(JsonValueKind.Object, root.GetProperty("format").ValueKind);
        Assert.True(root.GetProperty("format").TryGetProperty("properties", out _));
        Assert.Equal(0, root.GetProperty("options").GetProperty("temperature").GetInt32());
    }

    [Theory]
    [InlineData("this is not json")]
    [InlineData("null")]
    [InlineData("""{"purpose": 5}""")]
    public async Task Bad_response_text_throws_invalid_response(string text)
    {
        var handler = new FakeHandler((_, _, _) => Task.FromResult(Json(Envelope(text))));
        await Assert.ThrowsAsync<LlmInvalidResponseException>(
            () => Client(handler).CompleteJsonAsync<CodeSummary>("p", CodeSummarySchema.Json, CancellationToken.None));
    }

    [Fact]
    public async Task Envelope_without_response_throws_invalid_response()
    {
        var handler = new FakeHandler((_, _, _) => Task.FromResult(Json("""{"done":true}""")));
        await Assert.ThrowsAsync<LlmInvalidResponseException>(
            () => Client(handler).CompleteJsonAsync<CodeSummary>("p", CodeSummarySchema.Json, CancellationToken.None));
    }

    [Fact]
    public async Task Http_500_throws_LlmException_but_not_invalid_response()
    {
        var handler = new FakeHandler((_, _, _) => Task.FromResult(Json("oops", HttpStatusCode.InternalServerError)));
        var ex = await Assert.ThrowsAnyAsync<LlmException>(
            () => Client(handler).CompleteJsonAsync<CodeSummary>("p", CodeSummarySchema.Json, CancellationToken.None));
        Assert.IsNotType<LlmInvalidResponseException>(ex);
    }

    [Fact]
    public async Task HttpRequestException_throws_LlmException()
    {
        var handler = new FakeHandler((_, _, _) => throw new HttpRequestException("refused"));
        var ex = await Assert.ThrowsAnyAsync<LlmException>(
            () => Client(handler).CompleteJsonAsync<CodeSummary>("p", CodeSummarySchema.Json, CancellationToken.None));
        Assert.IsNotType<LlmInvalidResponseException>(ex);
    }

    [Fact]
    public async Task Timeout_throws_LlmException()
    {
        var handler = new FakeHandler((_, _, _) => throw new TaskCanceledException("timeout"));
        var ex = await Assert.ThrowsAnyAsync<LlmException>(
            () => Client(handler).CompleteJsonAsync<CodeSummary>("p", CodeSummarySchema.Json, CancellationToken.None));
        Assert.IsNotType<LlmInvalidResponseException>(ex);
    }

    [Fact]
    public async Task Cancelled_caller_token_propagates_OperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        var handler = new FakeHandler((_, _, ct) =>
        {
            cts.Cancel();
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(Json(Envelope(ValidReply)));
        });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Client(handler).CompleteJsonAsync<CodeSummary>("p", CodeSummarySchema.Json, cts.Token));
    }
}
