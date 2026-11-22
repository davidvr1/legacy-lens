using LegacyLens.Core.Llm;
using LegacyLens.Core.Migration;
using LegacyLens.Core.Model;

namespace LegacyLens.Infrastructure.Llm;

/// <summary>Offline stand-in for the language model. Makes no network calls and returns canned answers.</summary>
public sealed class DemoLlmClient : ILlmClient
{
    public static readonly CodeSummary DemoSummary = new(
        "Demo mode: this is a canned sample summary, not an analysis of your code. Set Llm:Provider to Ollama for a real summary. The sample page reads a user name from the query string, looks the user up in the database and writes a greeting.",
        ["QueryString: name", "Form: password", "Session: UserId"],
        ["HTML greeting with the user name", "Redirect to login.asp when the user is not found"],
        ["SELECT from Users by name (ADODB.Recordset)"],
        ["includes/db.inc"],
        ["ADODB.Connection", "ADODB.Recordset"]);

    /// <summary>A canned Razor Page migration. It must compile with RoslynCodeValidator (a test checks this).</summary>
    public const string DemoMigrationCode = """
        using System.Data.Common;
        using Microsoft.AspNetCore.Mvc;
        using Microsoft.AspNetCore.Mvc.RazorPages;

        namespace LegacyLens.Migrated.Pages;

        public sealed class GreetingModel(DbDataSource dataSource) : PageModel
        {
            public string? UserName { get; private set; }

            public async Task<IActionResult> OnGetAsync(string? name, CancellationToken cancellationToken)
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    return RedirectToPage("/Login");
                }

                await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT Name FROM Users WHERE Name = @name";

                var parameter = command.CreateParameter();
                parameter.ParameterName = "@name";
                parameter.Value = name;
                command.Parameters.Add(parameter);

                var found = await command.ExecuteScalarAsync(cancellationToken);
                if (found is null or DBNull)
                {
                    return RedirectToPage("/Login");
                }

                UserName = found.ToString();
                return Page();
            }
        }
        """;

    public static readonly MigrationReply DemoMigration = new(
        DemoMigrationCode,
        [
            "Demo mode: this is a canned sample migration, not a migration of your code. Set Llm:Provider to Ollama for a real migration.",
            "The query-string value is passed as a DbCommand parameter instead of being concatenated into the SQL (SQL injection fix).",
            "The connection comes from a DbDataSource registered in Program.cs, so no connection string is hard-coded.",
            "Check the table and column names (Users, Name) against your database before use.",
        ],
        []);

    public Task<T> CompleteJsonAsync<T>(string prompt, string jsonSchema, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<T>(cancellationToken);
        }

        if (typeof(T) == typeof(CodeSummary))
        {
            return Task.FromResult((T)(object)DemoSummary);
        }

        if (typeof(T) == typeof(MigrationReply))
        {
            // Cites the notes it was given, so every source it shows was in the prompt.
            var reply = DemoMigration with { Sources = MigrationPromptBuilder.ParseSourceIds(prompt) };
            return Task.FromResult((T)(object)reply);
        }

        // Faulted task rather than a synchronous throw, so callers see the same behavior as a real client.
        return Task.FromException<T>(new LlmException($"Demo mode has no canned response for {typeof(T).Name}."));
    }
}
