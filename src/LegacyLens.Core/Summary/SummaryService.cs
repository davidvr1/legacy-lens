using LegacyLens.Core.Llm;
using LegacyLens.Core.Model;

namespace LegacyLens.Core.Summary;

/// <summary>
/// Asks the language model for a <see cref="CodeSummary"/>. An invalid reply is retried once with the error
/// sent back to the model. Other LLM failures are not retried.
/// </summary>
public sealed class SummaryService(ILlmClient llmClient, SummarySettings settings) : ISummaryService
{
    public const string InvalidResponseError = "The AI summary is not available: the model returned an invalid response twice.";

    public const string UnavailableError = "The AI summary is not available: the language model could not be reached.";

    public async Task<SummaryResult> SummarizeAsync(SourceFile file, IReadOnlyList<Finding> findings, CancellationToken cancellationToken)
    {
        var prompt = SummaryPromptBuilder.Build(file, findings, settings.Variant);

        try
        {
            var (summary, error) = await AskAsync(prompt, cancellationToken);
            if (error is null)
            {
                return new SummaryResult(summary, null);
            }

            // Exactly one retry, with the validation or parse error included in the prompt.
            (summary, error) = await AskAsync(SummaryPromptBuilder.BuildRetry(prompt, error), cancellationToken);
            if (error is null)
            {
                return new SummaryResult(summary, null);
            }

            return new SummaryResult(null, InvalidResponseError);
        }
        catch (LlmException)
        {
            return new SummaryResult(null, UnavailableError);
        }
    }

    /// <summary>Makes one call. Returns the summary, or the error text when the reply is invalid.</summary>
    private async Task<(CodeSummary? Summary, string? Error)> AskAsync(string prompt, CancellationToken cancellationToken)
    {
        try
        {
            var reply = await llmClient.CompleteJsonAsync<CodeSummary>(prompt, CodeSummarySchema.Json, cancellationToken);
            var error = CodeSummaryValidator.Validate(reply);
            if (error is not null)
            {
                return (null, error);
            }

            return (reply, null);
        }
        catch (LlmInvalidResponseException ex)
        {
            return (null, ex.Message);
        }
    }
}
