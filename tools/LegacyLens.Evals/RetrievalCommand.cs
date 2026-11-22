using System.Globalization;
using System.Text.Json;
using LegacyLens.Core.Llm;
using LegacyLens.Core.Retrieval;
using LegacyLens.Infrastructure.Retrieval;
using Microsoft.Extensions.Options;

namespace LegacyLens.Evals;

/// <summary>Runs the retrieval eval for each requested mode and prints a Markdown report.</summary>
internal static class RetrievalCommand
{
    private const int EmbeddingTimeoutSeconds = 120;

    public static async Task<int> RunAsync(EvalArguments arguments, CancellationToken cancellationToken)
    {
        var root = FindRepoRoot(Directory.GetCurrentDirectory());
        if (root is null)
        {
            Console.Error.WriteLine("LegacyLens.sln was not found in the current directory or any parent. Run the command inside the repository.");
            return 1;
        }

        var knowledgePath = Path.Combine(root, "knowledge");
        var casesPath = Path.Combine(root, "evals", "retrieval.json");
        if (!Directory.Exists(knowledgePath))
        {
            Console.Error.WriteLine($"Knowledge folder not found: '{knowledgePath}'.");
            return 1;
        }

        if (!File.Exists(casesPath))
        {
            Console.Error.WriteLine($"Eval file not found: '{casesPath}'.");
            return 1;
        }

        IReadOnlyList<RetrievalEvalCase> cases;
        try
        {
            cases = RetrievalEvalCases.Parse(await File.ReadAllTextAsync(casesPath, cancellationToken));
        }
        catch (JsonException ex)
        {
            Console.Error.WriteLine($"Invalid eval file '{casesPath}': {ex.Message}");
            return 1;
        }

        var retrieval = new RetrievalOptions { KnowledgePath = knowledgePath, EmbeddingModel = arguments.EmbeddingModel };
        var knowledgeBase = new FileKnowledgeBase(Options.Create(retrieval));
        var bm25 = new Bm25Retriever(knowledgeBase, new Bm25Parameters(retrieval.K1, retrieval.B));

        using var http = new HttpClient
        {
            BaseAddress = new Uri(arguments.OllamaUrl.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(EmbeddingTimeoutSeconds),
        };
        var embedding = new EmbeddingRetriever(knowledgeBase, new OllamaEmbeddingClient(http, Options.Create(retrieval)));
        var hybrid = new HybridRetriever(bm25, embedding);

        var rows = new List<string>();
        var missesByMode = new List<(string Mode, List<string> Lines)>();
        foreach (var mode in arguments.Modes)
        {
            IRetriever retriever = mode switch
            {
                "bm25" => bm25,
                "embedding" => embedding,
                "hybrid" => hybrid,
                _ => throw new ArgumentOutOfRangeException(nameof(arguments), mode, "Unknown mode."),
            };

            RetrievalEvalResult result;
            try
            {
                result = await RetrievalEvaluator.RunAsync(retriever, cases, arguments.K, cancellationToken);
            }
            catch (LlmException ex)
            {
                Console.Error.WriteLine(
                    $"Mode '{mode}' skipped: {ex.Message} Check that Ollama runs at {arguments.OllamaUrl} with model '{arguments.EmbeddingModel}'.");
                continue;
            }

            rows.Add($"| {mode} | {result.Cases} | {Format(result.MeanRecallAtK)} | {Format(result.MeanReciprocalRank)} |");
            missesByMode.Add((mode, MissLines(cases, result, arguments.K)));
        }

        Console.WriteLine("| Mode | Cases | Recall@k | MRR |");
        Console.WriteLine("|---|---|---|---|");
        foreach (var row in rows)
        {
            Console.WriteLine(row);
        }

        foreach (var (mode, lines) in missesByMode)
        {
            Console.WriteLine();
            Console.WriteLine($"Misses for '{mode}' (recall < 1, top {arguments.K} shown):");
            foreach (var line in lines)
            {
                Console.WriteLine(line);
            }
        }

        return 0;
    }

    private static List<string> MissLines(IReadOnlyList<RetrievalEvalCase> cases, RetrievalEvalResult result, int k)
    {
        // PerCase is in the same order as the cases.
        var lines = new List<string>();
        foreach (var (evalCase, caseResult) in cases.Zip(result.PerCase))
        {
            if (caseResult.RecallAtK < 1)
            {
                var expected = string.Join(", ", evalCase.Expected);
                var retrieved = string.Join(", ", caseResult.Retrieved.Take(k));
                lines.Add($"- {evalCase.Id}: expected [{expected}]; retrieved [{retrieved}]");
            }
        }

        return lines;
    }

    private static string Format(double value) => value.ToString("F3", CultureInfo.InvariantCulture);

    private static string? FindRepoRoot(string start)
    {
        for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "LegacyLens.sln")))
            {
                return dir.FullName;
            }
        }

        return null;
    }
}
