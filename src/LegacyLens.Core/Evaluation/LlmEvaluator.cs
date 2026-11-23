using System.Diagnostics;
using LegacyLens.Core.Llm;
using LegacyLens.Core.Migration;
using LegacyLens.Core.Model;
using LegacyLens.Core.Retrieval;
using LegacyLens.Core.Rules;
using LegacyLens.Core.Summary;
using LegacyLens.Core.Validation;

namespace LegacyLens.Core.Evaluation;

/// <summary>Runs the summary and migration services on one evaluation case and scores their replies.</summary>
public sealed class LlmEvaluator(IRuleEngine ruleEngine, IRetriever retriever, ICodeValidator codeValidator)
{
    /// <summary>Runs the tasks that the options ask for. A task that fails is reported as an error, not thrown.</summary>
    public async Task<LlmCaseResult> RunCaseAsync(ILlmClient llmClient, LlmEvalCase evalCase, LlmEvalOptions options, CancellationToken cancellationToken)
    {
        var findings = ruleEngine.Run(evalCase.File);
        var summary = await RunSummaryAsync(llmClient, evalCase, findings, options, cancellationToken);
        var migration = await RunMigrationAsync(llmClient, evalCase, findings, options, cancellationToken);

        return new LlmCaseResult(
            evalCase.Id,
            summary.Score,
            summary.Error,
            summary.Duration,
            migration.Score,
            migration.Error,
            migration.Duration,
            summary.Raw,
            migration.Raw);
    }

    private static async Task<(SummaryScore? Score, string? Error, TimeSpan Duration, CodeSummary? Raw)> RunSummaryAsync(
        ILlmClient llmClient,
        LlmEvalCase evalCase,
        IReadOnlyList<Finding> findings,
        LlmEvalOptions options,
        CancellationToken cancellationToken)
    {
        if (!options.RunSummary)
        {
            return (null, null, TimeSpan.Zero, null);
        }

        var service = new SummaryService(llmClient, new SummarySettings(options.Variant));
        var stopwatch = Stopwatch.StartNew();
        var result = await service.SummarizeAsync(evalCase.File, findings, cancellationToken);
        stopwatch.Stop();

        if (result.Summary is null)
        {
            return (null, result.Error, stopwatch.Elapsed, null);
        }

        return (SummaryScorer.Score(result.Summary, evalCase.Facts, evalCase.File.Code), null, stopwatch.Elapsed, result.Summary);
    }

    private async Task<(MigrationScore? Score, string? Error, TimeSpan Duration, MigrationProposal? Raw)> RunMigrationAsync(
        ILlmClient llmClient,
        LlmEvalCase evalCase,
        IReadOnlyList<Finding> findings,
        LlmEvalOptions options,
        CancellationToken cancellationToken)
    {
        if (!options.RunMigration)
        {
            return (null, null, TimeSpan.Zero, null);
        }

        var recorder = new RecordingLlmClient(llmClient);
        var service = new MigrationService(recorder, retriever, codeValidator, new MigrationSettings(options.TopK, options.Variant));
        var stopwatch = Stopwatch.StartNew();
        var result = await service.ProposeAsync(evalCase.File, findings, cancellationToken);
        stopwatch.Stop();

        if (result.Proposal is null)
        {
            return (null, result.Error, stopwatch.Elapsed, null);
        }

        var score = MigrationScorer.Score(result.Proposal, CitedIds(recorder.Calls), PromptIds(recorder.Calls));
        return (score, null, stopwatch.Elapsed, result.Proposal);
    }

    /// <summary>The note ids that the first prompt offered. Empty when there were no calls.</summary>
    private static IReadOnlyList<string> PromptIds(IReadOnlyList<LlmCall> calls)
    {
        return calls.Count == 0 ? [] : MigrationPromptBuilder.ParseSourceIds(calls[0].Prompt);
    }

    /// <summary>The sources of the last migration reply that passed validation. Empty when there is none.</summary>
    private static IReadOnlyList<string> CitedIds(IReadOnlyList<LlmCall> calls)
    {
        var lastValid = calls
            .Select(call => call.Reply)
            .OfType<MigrationReply>()
            .LastOrDefault(reply => MigrationReplyValidator.Validate(reply) is null);

        return lastValid?.Sources ?? [];
    }
}
