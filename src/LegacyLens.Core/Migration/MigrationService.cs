using LegacyLens.Core.Llm;
using LegacyLens.Core.Model;
using LegacyLens.Core.Retrieval;
using LegacyLens.Core.Validation;

namespace LegacyLens.Core.Migration;

/// <summary>
/// Retrieves knowledge notes, asks the language model for a migration and checks that the code compiles.
/// An invalid reply is retried once. Code that does not compile gets one repair call. Failures are returned
/// as a <see cref="MigrationResult"/> error and never thrown, except cancellation.
/// </summary>
public sealed class MigrationService(
    ILlmClient llmClient,
    IRetriever retriever,
    ICodeValidator codeValidator,
    MigrationSettings settings) : IMigrationService
{
    public const string InvalidResponseError = "The migration proposal is not available: the model returned an invalid response twice.";

    public const string UnavailableError = "The migration proposal is not available: the language model could not be reached.";

    public async Task<MigrationResult> ProposeAsync(SourceFile file, IReadOnlyList<Finding> findings, CancellationToken cancellationToken)
    {
        try
        {
            var query = file.Code + "\n" + string.Join('\n', findings.Select(f => f.Title).Distinct());
            var chunks = await retriever.SearchAsync(query, settings.TopK, cancellationToken);
            var prompt = MigrationPromptBuilder.Build(file, findings, chunks, settings.Variant);

            var (reply, error) = await AskAsync(prompt, cancellationToken);
            if (reply is null)
            {
                // Exactly one retry, with the validation or parse error included in the prompt.
                (reply, error) = await AskAsync(MigrationPromptBuilder.BuildRetry(prompt, error), cancellationToken);
                if (reply is null)
                {
                    return new MigrationResult(null, InvalidResponseError);
                }
            }

            var validation = await codeValidator.ValidateAsync(reply.Code, cancellationToken);
            var repaired = false;
            if (!validation.Success)
            {
                // One repair call. Its code is used when the reply is valid, whatever the compile result.
                // When its reply is invalid, the first reply and its diagnostics are kept.
                var (repairReply, _) = await AskAsync(
                    MigrationPromptBuilder.BuildRepair(prompt, reply.Code, validation.Diagnostics),
                    cancellationToken);
                if (repairReply is not null)
                {
                    reply = repairReply;
                    validation = await codeValidator.ValidateAsync(reply.Code, cancellationToken);
                    repaired = true;
                }
            }

            var proposal = new MigrationProposal(
                reply.Code,
                reply.Notes,
                MapSources(reply.Sources, chunks),
                validation.Success,
                validation.Diagnostics,
                repaired);

            return new MigrationResult(proposal, null);
        }
        catch (LlmException)
        {
            return new MigrationResult(null, UnavailableError);
        }
    }

    /// <summary>Makes one call. Returns the reply, or the error text when the reply is invalid (empty when valid).</summary>
    private async Task<(MigrationReply? Reply, string Error)> AskAsync(string prompt, CancellationToken cancellationToken)
    {
        try
        {
            var reply = await llmClient.CompleteJsonAsync<MigrationReply>(prompt, MigrationSchema.Json, cancellationToken);
            var error = MigrationReplyValidator.Validate(reply);
            if (error is not null)
            {
                return (null, error);
            }

            return (reply, string.Empty);
        }
        catch (LlmInvalidResponseException ex)
        {
            return (null, ex.Message);
        }
    }

    /// <summary>Keeps only the cited ids that were in the prompt, case-insensitive and distinct, in the reply's order.</summary>
    private static IReadOnlyList<MigrationSource> MapSources(IReadOnlyList<string> ids, IReadOnlyList<KnowledgeChunk> chunks)
    {
        var sources = new List<MigrationSource>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in ids)
        {
            var chunk = chunks.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase));
            if (chunk is not null && seen.Add(chunk.Id))
            {
                sources.Add(new MigrationSource(chunk.Id, chunk.Title));
            }
        }

        return sources;
    }
}
