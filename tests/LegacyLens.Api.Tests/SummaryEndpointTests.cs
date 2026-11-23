using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LegacyLens.Core.Llm;
using LegacyLens.Core.Summary;
using LegacyLens.Infrastructure.Llm;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LegacyLens.Api.Tests;

public sealed class SummaryEndpointTests(ApiFactory factory)
    : IClassFixture<ApiFactory>
{
    private const string Code = "<%\nDim sql, n\nn = Request.QueryString(\"name\")\nsql = \"SELECT * FROM U WHERE N = '\" & n & \"'\"\n%>\n";

    private sealed class ThrowingLlmClient : ILlmClient
    {
        public Task<T> CompleteJsonAsync<T>(string prompt, string jsonSchema, CancellationToken cancellationToken)
            => throw new LlmException("down");
    }

    private static async Task<JsonElement> Post(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/analyses", new { fileName = "a.asp", language = "classic-asp", code = Code });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).Clone();
    }

    private static List<string> Strings(JsonElement e) => e.EnumerateArray().Select(x => x.GetString()!).ToList();

    [Fact]
    public async Task Demo_mode_returns_demo_summary_and_findings()
    {
        using var client = factory.CreateClient();
        var json = await Post(client);
        var demo = DemoLlmClient.DemoSummary;

        var summary = json.GetProperty("summary");
        Assert.Equal(demo.Purpose, summary.GetProperty("purpose").GetString());
        Assert.Equal(demo.Inputs, Strings(summary.GetProperty("inputs")));
        Assert.Equal(demo.Outputs, Strings(summary.GetProperty("outputs")));
        Assert.Equal(demo.DatabaseAccess, Strings(summary.GetProperty("databaseAccess")));
        Assert.Equal(demo.Includes, Strings(summary.GetProperty("includes")));
        Assert.Equal(demo.ComObjects, Strings(summary.GetProperty("comObjects")));
        Assert.Equal(JsonValueKind.Null, json.GetProperty("summaryError").ValueKind);
        Assert.Contains(json.GetProperty("findings").EnumerateArray(), f => f.GetProperty("ruleId").GetString() == "SQL001");
    }

    [Fact]
    public async Task Llm_failure_gives_200_with_findings_and_summary_error()
    {
        using var custom = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.RemoveAll<ILlmClient>();
            s.AddSingleton<ILlmClient, ThrowingLlmClient>();
        }));
        using var client = custom.CreateClient();
        var json = await Post(client);

        Assert.Equal(JsonValueKind.Null, json.GetProperty("summary").ValueKind);
        Assert.Equal(SummaryService.UnavailableError, json.GetProperty("summaryError").GetString());
        Assert.Contains(json.GetProperty("findings").EnumerateArray(), f => f.GetProperty("ruleId").GetString() == "SQL001");
    }
}
