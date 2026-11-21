using LegacyLens.Core.Llm;
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

        // Faulted task rather than a synchronous throw, so callers see the same behavior as a real client.
        return Task.FromException<T>(new LlmException($"Demo mode has no canned response for {typeof(T).Name}."));
    }
}
