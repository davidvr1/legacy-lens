using LegacyLens.Core.Migration;
using LegacyLens.Infrastructure.Llm;
using LegacyLens.Infrastructure.Validation;

namespace LegacyLens.Api.Tests;

public sealed class RoslynCodeValidatorTests
{
    [Fact]
    public async Task Demo_migration_code_compiles()
    {
        var reply = await new DemoLlmClient().CompleteJsonAsync<MigrationReply>("p", "{}", CancellationToken.None);

        var result = await new RoslynCodeValidator().ValidateAsync(reply.Code, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Empty(result.Diagnostics);
    }

    private static string Describe(LegacyLens.Core.Validation.CodeValidationResult r)
        => string.Join("; ", r.Diagnostics.Select(d => $"{d.Line} {d.Id} {d.Message}"));

    [Fact]
    public async Task PageModel_with_DbDataSource_compiles()
    {
        const string code = """
            using System.Data.Common;
            using Microsoft.AspNetCore.Mvc;
            using Microsoft.AspNetCore.Mvc.RazorPages;

            namespace Sample;

            public class LoginModel(DbDataSource dataSource) : PageModel
            {
                public string? Name { get; private set; }

                public async Task<IActionResult> OnGetAsync(string name, CancellationToken ct)
                {
                    await using var cmd = dataSource.CreateCommand("SELECT Name FROM Users WHERE Name = @n");
                    var p = cmd.CreateParameter();
                    p.ParameterName = "@n";
                    p.Value = name;
                    cmd.Parameters.Add(p);
                    Name = (await cmd.ExecuteScalarAsync(ct)) as string;
                    return Name is null ? RedirectToPage("/Index") : Page();
                }
            }
            """;

        var result = await new RoslynCodeValidator().ValidateAsync(code, CancellationToken.None);

        Assert.True(result.Success, Describe(result));
    }

    [Fact]
    public async Task Controller_with_DbDataSource_compiles()
    {
        const string code = """
            using System.Data.Common;
            using Microsoft.AspNetCore.Mvc;

            namespace Sample;

            [ApiController]
            [Route("api/items")]
            public class ItemsController(DbDataSource dataSource, IConfiguration config) : ControllerBase
            {
                [HttpGet]
                public async Task<IActionResult> Get(CancellationToken ct)
                {
                    await using var conn = await dataSource.OpenConnectionAsync(ct);
                    await using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT 1";
                    var value = await cmd.ExecuteScalarAsync(ct);
                    return Ok(new { value, hasKey = config["Key"] is not null });
                }
            }
            """;

        var result = await new RoslynCodeValidator().ValidateAsync(code, CancellationToken.None);

        Assert.True(result.Success, Describe(result));
    }

    [Fact]
    public async Task Syntax_error_reports_its_line()
    {
        const string code = "namespace N;\n\npublic class X\n{\n    void M() { int a = ; }\n}\n";

        var result = await new RoslynCodeValidator().ValidateAsync(code, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Line == 5);
        Assert.All(result.Diagnostics, d => Assert.True(d.Line >= 1));
    }

    [Fact]
    public async Task Diagnostics_are_capped_at_20_and_sorted_by_line()
    {
        var body = string.Join("\n", Enumerable.Range(1, 30).Select(i => $"    void M{i}() {{ missing{i}(); }}"));
        var code = "public class X\n{\n" + body + "\n}\n";

        var result = await new RoslynCodeValidator().ValidateAsync(code, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(20, result.Diagnostics.Count);
        Assert.Equal(result.Diagnostics.Select(d => d.Line).OrderBy(l => l), result.Diagnostics.Select(d => d.Line));
    }

    [Fact]
    public async Task Global_usings_make_Task_resolve_without_a_using()
    {
        const string code = "public class X { public Task<int> M() => Task.FromResult(1); }";

        var result = await new RoslynCodeValidator().ValidateAsync(code, CancellationToken.None);

        Assert.True(result.Success, Describe(result));
    }

    [Fact]
    public async Task Duplicate_using_of_a_global_namespace_is_not_an_error()
    {
        const string code = "using System.Threading.Tasks;\npublic class X { public Task M() => Task.CompletedTask; }";

        var result = await new RoslynCodeValidator().ValidateAsync(code, CancellationToken.None);

        Assert.True(result.Success, Describe(result));
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public async Task Code_with_only_warnings_succeeds()
    {
        const string code = "public class X { void M() { int unused = 1; } }";

        var result = await new RoslynCodeValidator().ValidateAsync(code, CancellationToken.None);

        Assert.True(result.Success, Describe(result));
    }

    [Fact]
    public async Task Cancelled_token_throws()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new RoslynCodeValidator().ValidateAsync("class X {}", cts.Token));
    }

    [Fact]
    public async Task Undefined_name_gives_CS0103_on_line_1()
    {
        var result = await new RoslynCodeValidator().ValidateAsync(
            "public class X { void M() { undefinedThing(); } }",
            CancellationToken.None);

        Assert.False(result.Success);
        var diagnostic = Assert.Single(result.Diagnostics, d => d.Id == "CS0103");
        Assert.Equal(1, diagnostic.Line);
    }
}
