using System.Net;
using System.Text;
using System.Text.Json;
using LegacyLens.Core.Llm;
using LegacyLens.Infrastructure.Retrieval;
using Microsoft.Extensions.Options;

namespace LegacyLens.Api.Tests;

public sealed class OllamaEmbeddingClientTests
{
    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK)
        => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static OllamaEmbeddingClient Client(FakeHandler handler, string model = "test-embed") => new(
        new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434/") },
        Options.Create(new RetrievalOptions { EmbeddingModel = model }));

    [Fact]
    public async Task Sends_model_and_input_and_parses_vectors()
    {
        var handler = new FakeHandler((_, _, _) => Task.FromResult(Json("""{"embeddings":[[0.5,1.0],[2.0,-3.5]]}""")));

        var vectors = await Client(handler).EmbedAsync(["one", "two"], CancellationToken.None);

        Assert.Equal(2, vectors.Count);
        Assert.Equal([0.5f, 1.0f], vectors[0]);
        Assert.Equal([2.0f, -3.5f], vectors[1]);
        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal("http://localhost:11434/api/embed", handler.Request.RequestUri!.ToString());

        using var doc = JsonDocument.Parse(handler.Body!);
        Assert.Equal("test-embed", doc.RootElement.GetProperty("model").GetString());
        Assert.Equal(["one", "two"], doc.RootElement.GetProperty("input").EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public async Task Empty_input_returns_empty_without_http_call()
    {
        var handler = new FakeHandler((_, _, _) => throw new InvalidOperationException("must not be called"));

        var vectors = await Client(handler).EmbedAsync([], CancellationToken.None);

        Assert.Empty(vectors);
        Assert.Null(handler.Request);
    }

    [Theory]
    [InlineData("""{"embeddings":[[1.0]]}""")]
    [InlineData("""{"embeddings":[[1.0],[2.0],[3.0]]}""")]
    [InlineData("""{"embeddings":[]}""")]
    [InlineData("""{"other":1}""")]
    [InlineData("""{"embeddings":null}""")]
    [InlineData("null")]
    public async Task Count_mismatch_or_missing_embeddings_throws_invalid_response(string body)
    {
        var handler = new FakeHandler((_, _, _) => Task.FromResult(Json(body)));
        await Assert.ThrowsAsync<LlmInvalidResponseException>(() => Client(handler).EmbedAsync(["a", "b"], CancellationToken.None));
    }

    [Theory]
    [InlineData("""{"embeddings":[[]]}""")]
    [InlineData("""{"embeddings":[null]}""")]
    public async Task Empty_vector_throws_invalid_response(string body)
    {
        var handler = new FakeHandler((_, _, _) => Task.FromResult(Json(body)));
        await Assert.ThrowsAsync<LlmInvalidResponseException>(() => Client(handler).EmbedAsync(["a"], CancellationToken.None));
    }

    [Theory]
    [InlineData("this is not json")]
    [InlineData("""{"embeddings":"nope"}""")]
    [InlineData("""{"embeddings":[["x"]]}""")]
    public async Task Bad_json_throws_invalid_response(string body)
    {
        var handler = new FakeHandler((_, _, _) => Task.FromResult(Json(body)));
        await Assert.ThrowsAsync<LlmInvalidResponseException>(() => Client(handler).EmbedAsync(["a"], CancellationToken.None));
    }

    [Fact]
    public async Task Http_500_throws_LlmException_but_not_invalid_response()
    {
        var handler = new FakeHandler((_, _, _) => Task.FromResult(Json("oops", HttpStatusCode.InternalServerError)));
        var ex = await Assert.ThrowsAnyAsync<LlmException>(() => Client(handler).EmbedAsync(["a"], CancellationToken.None));
        Assert.IsNotType<LlmInvalidResponseException>(ex);
    }

    [Fact]
    public async Task HttpRequestException_throws_LlmException()
    {
        var handler = new FakeHandler((_, _, _) => throw new HttpRequestException("refused"));
        var ex = await Assert.ThrowsAnyAsync<LlmException>(() => Client(handler).EmbedAsync(["a"], CancellationToken.None));
        Assert.IsNotType<LlmInvalidResponseException>(ex);
    }

    [Fact]
    public async Task Timeout_throws_LlmException()
    {
        var handler = new FakeHandler((_, _, _) => throw new TaskCanceledException("timeout"));
        var ex = await Assert.ThrowsAnyAsync<LlmException>(() => Client(handler).EmbedAsync(["a"], CancellationToken.None));
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
            return Task.FromResult(Json("""{"embeddings":[[1.0]]}"""));
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Client(handler).EmbedAsync(["a"], cts.Token));
    }
}
