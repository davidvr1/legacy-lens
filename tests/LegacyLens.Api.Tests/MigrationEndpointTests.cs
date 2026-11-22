using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LegacyLens.Api.Controllers;
using Microsoft.AspNetCore.Mvc.Testing;

namespace LegacyLens.Api.Tests;

public sealed class MigrationEndpointTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private const string SqlInjectionSnippet =
        "<%\n" +
        "Dim sql, userName\n" +
        "userName = Request.QueryString(\"name\")\n" +
        "sql = \"SELECT * FROM Users WHERE Name = '\" & userName & \"'\"\n" +
        "%>\n";

    [Fact]
    public async Task Demo_mode_returns_a_compiling_migration_with_at_least_one_source()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/analyses",
            new AnalyzeRequest("inject.asp", "classic-asp", SqlInjectionSnippet));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        var migration = root.GetProperty("migration");
        Assert.NotEqual(JsonValueKind.Null, migration.ValueKind);
        Assert.True(migration.GetProperty("compilesOk").GetBoolean());
        Assert.NotEmpty(migration.GetProperty("sources").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("migrationError").ValueKind);
    }
}
