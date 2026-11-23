using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using LegacyLens.Api.Controllers;
using Microsoft.AspNetCore.Mvc.Testing;

namespace LegacyLens.Api.Tests;

public sealed class AnalysesEndpointTests(ApiFactory factory)
    : IClassFixture<ApiFactory>
{
    // Used when a response may contain a Finding (Severity is a string like "High" thanks to
    // JsonStringEnumConverter on the server; the default client options do not know that).
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private const string SqlInjectionSnippet =
        "<%\n" +
        "Dim sql, userName\n" +
        "userName = Request.QueryString(\"name\")\n" +
        "sql = \"SELECT * FROM Users WHERE Name = '\" & userName & \"'\"\n" +
        "%>\n";

    private const string WebFormsSqlInjectionSnippet =
        "using System;\n" +
        "public class Foo\n" +
        "{\n" +
        "    protected void Page_Load(object sender, EventArgs e)\n" +
        "    {\n" +
        "        string sql;\n" +
        "        sql = \"SELECT * FROM Users WHERE Id = \" + Request.QueryString[\"id\"];\n" +
        "    }\n" +
        "}\n";

    private const string CleanSnippet =
        "<%\n" +
        "Dim total\n" +
        "total = 1 + 2\n" +
        "Response.Write total\n" +
        "%>\n";

    [Fact]
    public async Task Sql_injection_snippet_returns_200_with_SQL001_at_the_right_line_and_high_severity_as_string()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/analyses",
            new AnalyzeRequest("inject.asp", "classic-asp", SqlInjectionSnippet));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        var findings = document.RootElement.GetProperty("findings");

        var sqlFinding = findings.EnumerateArray()
            .Single(f => f.GetProperty("ruleId").GetString() == "SQL001");

        Assert.Equal(4, sqlFinding.GetProperty("line").GetInt32());
        Assert.Equal(JsonValueKind.String, sqlFinding.GetProperty("severity").ValueKind);
        Assert.Equal("High", sqlFinding.GetProperty("severity").GetString());
    }

    [Fact]
    public async Task WebForms_codebehind_snippet_returns_expected_SQL001_finding()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/analyses",
            new AnalyzeRequest("CustomerSearch.aspx.cs", "webforms", WebFormsSqlInjectionSnippet));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<AnalysisResponse>(JsonOptions);

        Assert.NotNull(result);
        var sqlFinding = Assert.Single(result!.Findings, f => f.RuleId == "SQL001");
        Assert.Equal(7, sqlFinding.Line);
        Assert.Equal(LegacyLens.Core.Model.Severity.High, sqlFinding.Severity);
    }

    [Fact]
    public async Task Clean_code_returns_200_with_no_findings()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/analyses",
            new AnalyzeRequest("clean.asp", "classic-asp", CleanSnippet));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<AnalysisResponse>(JsonOptions);

        Assert.NotNull(result);
        Assert.Empty(result!.Findings);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Empty_whitespace_or_null_code_returns_400_with_error(string? code)
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/analyses",
            new AnalyzeRequest("x.asp", "classic-asp", code));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
        Assert.NotNull(error);
        Assert.False(string.IsNullOrWhiteSpace(error!.Error));
    }

    [Fact]
    public async Task Code_over_200000_characters_returns_400()
    {
        using var client = factory.CreateClient();
        var tooLong = new string('x', AnalysesController.MaxCodeLength + 1);

        var response = await client.PostAsJsonAsync(
            "/api/analyses",
            new AnalyzeRequest("big.asp", "classic-asp", tooLong));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
        Assert.NotNull(error);
        Assert.False(string.IsNullOrWhiteSpace(error!.Error));
    }

    [Fact]
    public async Task Code_of_exactly_200000_safe_characters_returns_200()
    {
        using var client = factory.CreateClient();
        var atLimit = new string('x', AnalysesController.MaxCodeLength);

        var response = await client.PostAsJsonAsync(
            "/api/analyses",
            new AnalyzeRequest("at-limit.asp", "classic-asp", atLimit));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AnalysisResponse>(JsonOptions);
        Assert.NotNull(result);
        Assert.Empty(result!.Findings);
    }

    [Theory]
    [InlineData("php")]
    [InlineData("ClassicASP")]
    [InlineData("")]
    public async Task Unknown_language_returns_400(string language)
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/analyses",
            new AnalyzeRequest("x.asp", language, "some code"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
        Assert.NotNull(error);
        Assert.False(string.IsNullOrWhiteSpace(error!.Error));
    }

    [Fact]
    public async Task Missing_language_returns_400()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/analyses",
            new AnalyzeRequest("x.asp", null, "some code"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
        Assert.NotNull(error);
        Assert.False(string.IsNullOrWhiteSpace(error!.Error));
    }

    [Fact]
    public async Task Missing_file_name_defaults_to_pasted_code()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/analyses",
            new AnalyzeRequest(null, "classic-asp", CleanSnippet));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AnalysisResponse>(JsonOptions);
        Assert.NotNull(result);
        Assert.Equal("pasted-code", result!.FileName);
    }

    [Theory]
    [InlineData("CLASSIC-ASP")]
    [InlineData("Classic-Asp")]
    [InlineData("WEBFORMS")]
    [InlineData("WebForms")]
    public async Task Language_is_case_insensitive(string language)
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/analyses",
            new AnalyzeRequest("x.asp", language, CleanSnippet));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AnalysisResponse>(JsonOptions);
        Assert.NotNull(result);
        Assert.Equal(language.ToLowerInvariant(), result!.Language);
    }
}
