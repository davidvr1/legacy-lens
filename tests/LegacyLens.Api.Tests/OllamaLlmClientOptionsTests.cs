using System.Net;
using System.Text;
using System.Text.Json;
using LegacyLens.Core.Model;
using LegacyLens.Core.Summary;
using LegacyLens.Infrastructure.Llm;
using Microsoft.Extensions.Options;

namespace LegacyLens.Api.Tests;

public sealed class OllamaLlmClientOptionsTests
{
    private const string Reply =
        """{"purpose":"p","inputs":[],"outputs":[],"databaseAccess":[],"includes":[],"comObjects":[]}""";

    private static async Task<JsonElement> SendAsync(LlmOptions options)
    {
        var handler = new FakeHandler((_, _, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(new { response = Reply, done = true }), Encoding.UTF8, "application/json"),
        }));
        var client = new OllamaLlmClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434/") }, Options.Create(options));

        await client.CompleteJsonAsync<CodeSummary>("p", CodeSummarySchema.Json, CancellationToken.None);

        return JsonDocument.Parse(handler.Body!).RootElement.GetProperty("options").Clone();
    }

    [Fact]
    public async Task Sends_temperature_top_p_and_num_ctx_from_options()
    {
        var o = await SendAsync(new LlmOptions { Temperature = 0.3, TopP = 0.8, NumCtx = 4096 });

        Assert.Equal(0.3, o.GetProperty("temperature").GetDouble(), 10);
        Assert.Equal(0.8, o.GetProperty("top_p").GetDouble(), 10);
        Assert.Equal(4096, o.GetProperty("num_ctx").GetInt32());
    }

    [Fact]
    public async Task Sends_defaults_0_1_and_8192()
    {
        var o = await SendAsync(new LlmOptions());

        Assert.Equal(0, o.GetProperty("temperature").GetDouble());
        Assert.Equal(1.0, o.GetProperty("top_p").GetDouble());
        Assert.Equal(8192, o.GetProperty("num_ctx").GetInt32());
    }

    [Fact]
    public void LlmOptions_defaults()
    {
        var o = new LlmOptions();
        Assert.Equal(0, o.Temperature);
        Assert.Equal(1.0, o.TopP);
        Assert.Equal(8192, o.NumCtx);
    }
}
