using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LegacyLens.Api.Controllers;

namespace LegacyLens.Api.Tests;

public sealed class FileNameLimitTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Code = "<% Response.Write \"hi\" %>";

    private static Task<HttpResponseMessage> Post(HttpClient client, string fileName) =>
        client.PostAsJsonAsync("/api/analyses", new AnalyzeRequest(fileName, "classic-asp", Code));

    private static async Task<List<string>> HistoryIds(HttpClient client)
    {
        var items = await client.GetFromJsonAsync<JsonElement>("/api/analyses");
        return items.EnumerateArray().Select(i => i.GetProperty("id").GetString()!).ToList();
    }

    [Fact]
    public async Task File_name_of_exactly_260_characters_is_accepted()
    {
        using var client = factory.CreateClient();
        var name = new string('a', 256) + ".asp";
        Assert.Equal(260, name.Length);

        var response = await Post(client, name);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(name, body.GetProperty("fileName").GetString());
    }

    [Fact]
    public async Task File_name_of_261_characters_returns_400_with_error()
    {
        using var client = factory.CreateClient();
        var name = new string('a', 257) + ".asp";
        Assert.Equal(261, name.Length);

        var response = await Post(client, name);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.False(string.IsNullOrWhiteSpace(error?.Error));
    }

    [Fact]
    public async Task File_name_length_is_measured_after_trimming()
    {
        using var client = factory.CreateClient();
        var name = "  " + new string('b', 256) + ".asp  ";

        var response = await Post(client, name);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Rejected_request_is_not_saved_in_history()
    {
        using var client = factory.CreateClient();
        var before = await HistoryIds(client);

        var tooLong = await Post(client, new string('c', 261));
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);

        var after = await HistoryIds(client);
        Assert.Equal(before, after);
    }
}
