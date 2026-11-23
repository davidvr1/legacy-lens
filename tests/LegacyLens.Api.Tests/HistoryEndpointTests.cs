using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LegacyLens.Api.Controllers;

namespace LegacyLens.Api.Tests;

public sealed class HistoryEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string SqlInjectionSnippet =
        "<%\n" +
        "Dim sql, userName\n" +
        "userName = Request.QueryString(\"name\")\n" +
        "sql = \"SELECT * FROM Users WHERE Name = '\" & userName & \"'\"\n" +
        "%>\n";

    private static async Task<JsonElement> PostAnalysis(HttpClient client, string fileName)
    {
        var response = await client.PostAsJsonAsync(
            "/api/analyses",
            new AnalyzeRequest(fileName, "classic-asp", SqlInjectionSnippet));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).Clone();
    }

    [Fact]
    public async Task Saved_analysis_appears_in_history_with_its_file_name_language_and_counts()
    {
        using var client = factory.CreateClient();
        var posted = await PostAnalysis(client, "history-inject.asp");

        var items = await client.GetFromJsonAsync<JsonElement>("/api/analyses");
        var item = items.EnumerateArray().Single(i => i.GetProperty("id").GetString() == posted.GetProperty("id").GetString());

        Assert.Equal("history-inject.asp", item.GetProperty("fileName").GetString());
        Assert.Equal("classic-asp", item.GetProperty("language").GetString());

        var highFindings = posted.GetProperty("findings").EnumerateArray()
            .Count(f => f.GetProperty("severity").GetString() == "High");
        var counts = item.GetProperty("findingCounts");
        Assert.Equal(highFindings, counts.GetProperty("high").GetInt32());
        Assert.True(highFindings >= 1);
    }

    [Fact]
    public async Task History_is_newest_first()
    {
        using var client = factory.CreateClient();
        await PostAnalysis(client, "history-order-1.asp");
        await PostAnalysis(client, "history-order-2.asp");

        var items = await client.GetFromJsonAsync<JsonElement>("/api/analyses");
        var createdAt = items.EnumerateArray().Select(i => i.GetProperty("createdAt").GetDateTimeOffset()).ToList();

        Assert.Equal(createdAt.OrderByDescending(d => d).ToList(), createdAt);
    }

    [Fact]
    public async Task Get_by_id_returns_the_same_findings_summary_and_migration_as_the_post()
    {
        using var client = factory.CreateClient();
        var posted = await PostAnalysis(client, "history-detail.asp");

        var response = await client.GetAsync($"/api/analyses/{posted.GetProperty("id").GetString()}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var loaded = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.NotEqual(JsonValueKind.Null, loaded.GetProperty("migration").ValueKind);
        Assert.NotEqual(JsonValueKind.Null, loaded.GetProperty("summary").ValueKind);
        foreach (var name in new[] { "id", "fileName", "language", "createdAt", "findings", "summary", "summaryError", "migration", "migrationError" })
        {
            Assert.Equal(posted.GetProperty(name).GetRawText(), loaded.GetProperty(name).GetRawText());
        }

        var sqlFinding = loaded.GetProperty("findings").EnumerateArray().Single(f => f.GetProperty("ruleId").GetString() == "SQL001");
        Assert.Equal(4, sqlFinding.GetProperty("line").GetInt32());
    }

    [Fact]
    public async Task Unknown_id_returns_404_with_error()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/analyses/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("Analysis not found.", error?.Error);
    }

    [Fact]
    public async Task Non_guid_id_returns_404()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/analyses/not-a-guid");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
