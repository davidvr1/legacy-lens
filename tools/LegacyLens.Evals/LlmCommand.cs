using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LegacyLens.Core.Evaluation;
using LegacyLens.Core.Llm;
using LegacyLens.Core.Retrieval;
using LegacyLens.Core.Rules;
using LegacyLens.Infrastructure.Llm;
using LegacyLens.Infrastructure.Retrieval;
using LegacyLens.Infrastructure.Validation;
using Microsoft.Extensions.Options;

namespace LegacyLens.Evals;

/// <summary>Runs every config of the grid over the eval cases and writes the results to evals/results.</summary>
internal static class LlmCommand
{
    private const int MaxShortTextLength = 80;

    private static readonly JsonSerializerOptions WriteOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly JsonSerializerOptions ReadOptions = new(JsonSerializerDefaults.Web);

    public static async Task<int> RunAsync(LlmEvalArguments arguments, CancellationToken cancellationToken)
    {
        // The table uses ±, ✓ and ✗, which the default Windows console code page cannot show.
        Console.OutputEncoding = Encoding.UTF8;

        var root = RepoRoot.Find(Directory.GetCurrentDirectory());
        if (root is null)
        {
            Console.Error.WriteLine("LegacyLens.sln was not found in the current directory or any parent. Run the command inside the repository.");
            return 1;
        }

        var knowledgePath = Path.Combine(root, "knowledge");
        var casesPath = Path.Combine(root, "evals", "llm");
        var resultsPath = Path.Combine(root, "evals", "results");
        if (!Directory.Exists(knowledgePath))
        {
            Console.Error.WriteLine($"Knowledge folder not found: '{knowledgePath}'.");
            return 1;
        }

        if (!Directory.Exists(casesPath))
        {
            Console.Error.WriteLine($"Cases folder not found: '{casesPath}'.");
            return 1;
        }

        IReadOnlyList<LlmEvalCase> cases;
        try
        {
            cases = SelectCases(await LoadCasesAsync(casesPath, cancellationToken), arguments.CaseIds);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        var knowledgeBase = new FileKnowledgeBase(Options.Create(new RetrievalOptions { KnowledgePath = knowledgePath }));
        var configs = BuildConfigs(arguments);
        var startedAt = DateTimeOffset.Now;
        var fileName = $"{startedAt.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}-{arguments.Label}";
        var jsonPath = Path.Combine(resultsPath, fileName + ".json");
        var markdownPath = Path.Combine(resultsPath, fileName + ".md");
        var caseIds = cases.Select(evalCase => evalCase.Id).ToList();
        var completed = new List<LlmConfigReport>();

        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        ConsoleCancelEventHandler onCancel = (_, eventArgs) =>
        {
            // Keep the process alive long enough to write the completed configs.
            eventArgs.Cancel = true;
            stop.Cancel();
            Console.Error.WriteLine("Stopping. The completed configs are kept.");
        };

        Console.CancelKeyPress += onCancel;
        try
        {
            if (!await CheckOllamaAsync(arguments, stop.Token))
            {
                return 1;
            }

            var position = 0;
            foreach (var model in arguments.Models)
            {
                using var http = CreateClient(arguments.OllamaUrl, arguments.TimeoutSeconds);
                await WarmUpAsync(http, model, arguments, stop.Token);

                foreach (var config in configs.Where(candidate => candidate.Model == model))
                {
                    position++;
                    var report = await RunConfigAsync(http, knowledgeBase, cases, config, arguments, position, configs.Count, stop.Token);
                    completed.Add(report);
                    await WriteResultsAsync(new LlmEvalReport(startedAt, arguments, caseIds, completed), resultsPath, jsonPath, markdownPath);
                }
            }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested)
        {
            Console.Error.WriteLine($"Stopped early: {completed.Count} of {configs.Count} configs completed.");
        }
        finally
        {
            Console.CancelKeyPress -= onCancel;
        }

        Console.WriteLine(LlmReport.Markdown(completed));
        if (completed.Count > 0)
        {
            Console.Error.WriteLine($"Results written to {jsonPath} and {markdownPath}");
        }

        return 0;
    }

    /// <summary>Reads every case folder under evals/llm, sorted by id. Each folder needs expected.json and one input.* file.</summary>
    private static async Task<IReadOnlyList<LlmEvalCase>> LoadCasesAsync(string casesPath, CancellationToken cancellationToken)
    {
        var cases = new List<LlmEvalCase>();
        foreach (var folder in Directory.GetDirectories(casesPath).OrderBy(dir => Path.GetFileName(dir), StringComparer.Ordinal))
        {
            var id = Path.GetFileName(folder);
            var expectedPath = Path.Combine(folder, "expected.json");
            var inputs = Directory.GetFiles(folder, "input.*");
            if (!File.Exists(expectedPath) || inputs.Length != 1)
            {
                throw new ArgumentException($"Case '{id}' needs one expected.json and exactly one input.* file.");
            }

            try
            {
                var code = await File.ReadAllTextAsync(inputs[0], cancellationToken);
                var expectedJson = await File.ReadAllTextAsync(expectedPath, cancellationToken);
                cases.Add(LlmEvalCaseParser.Parse(id, Path.GetFileName(inputs[0]), code, expectedJson));
            }
            catch (Exception ex) when (ex is ArgumentException or JsonException)
            {
                throw new ArgumentException($"Case '{id}': {ex.Message}", ex);
            }
        }

        if (cases.Count == 0)
        {
            throw new ArgumentException($"No cases found in '{casesPath}'.");
        }

        return cases;
    }

    private static IReadOnlyList<LlmEvalCase> SelectCases(IReadOnlyList<LlmEvalCase> all, IReadOnlyList<string>? ids)
    {
        if (ids is null)
        {
            return all;
        }

        var unknown = ids.Where(id => all.All(evalCase => evalCase.Id != id)).ToList();
        if (unknown.Count > 0)
        {
            throw new ArgumentException($"Unknown case id(s): {string.Join(", ", unknown)}. The cases are the folders under evals/llm.");
        }

        return all.Where(evalCase => ids.Contains(evalCase.Id)).ToList();
    }

    /// <summary>The grid: models x temperatures x top-p x variants x top-k x bm25, models outermost.</summary>
    private static IReadOnlyList<LlmConfig> BuildConfigs(LlmEvalArguments arguments)
    {
        return (from model in arguments.Models
                from temperature in arguments.Temperatures
                from topP in arguments.TopPs
                from variant in arguments.Variants
                from topK in arguments.TopKs
                from bm25 in arguments.Bm25s
                select new LlmConfig(model, temperature, topP, variant, topK, bm25))
            .ToList();
    }

    /// <summary>Asks Ollama for its models. Prints the reason and returns false when Ollama is unreachable or a model is missing.</summary>
    private static async Task<bool> CheckOllamaAsync(LlmEvalArguments arguments, CancellationToken cancellationToken)
    {
        var url = arguments.OllamaUrl;
        HashSet<string> installed;
        using (var http = CreateClient(url, arguments.TimeoutSeconds))
        {
            try
            {
                using var response = await http.GetAsync("api/tags", cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    Console.Error.WriteLine($"Ollama at {url} returned HTTP {(int)response.StatusCode} for api/tags.");
                    return false;
                }

                var tags = await response.Content.ReadFromJsonAsync<TagsReply>(ReadOptions, cancellationToken);
                installed = (tags?.Models ?? []).Select(model => model.Name).OfType<string>().ToHashSet(StringComparer.Ordinal);
            }
            catch (HttpRequestException)
            {
                Console.Error.WriteLine($"Ollama is not reachable at {url}. Start it with: ollama serve");
                return false;
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                Console.Error.WriteLine($"Ollama at {url} did not answer in time.");
                return false;
            }
            catch (JsonException)
            {
                Console.Error.WriteLine($"Ollama at {url} returned an unexpected reply for api/tags.");
                return false;
            }
        }

        // A name without a tag also matches its ":latest" tag, as Ollama does.
        bool IsInstalled(string model) => installed.Contains(model) || (!model.Contains(':') && installed.Contains($"{model}:latest"));

        var missing = arguments.Models.Where(model => !IsInstalled(model)).ToList();
        foreach (var model in missing)
        {
            Console.Error.WriteLine($"Model '{model}' is not installed. Run: ollama pull {model}");
        }

        return missing.Count == 0;
    }

    /// <summary>One call before timing, so loading the model is not counted in the latency. Its result and errors are ignored.</summary>
    private static async Task WarmUpAsync(HttpClient http, string model, LlmEvalArguments arguments, CancellationToken cancellationToken)
    {
        var client = new OllamaLlmClient(http, Options.Create(new LlmOptions
        {
            Provider = "Ollama",
            Model = model,
            OllamaUrl = arguments.OllamaUrl,
            TimeoutSeconds = arguments.TimeoutSeconds,
            NumCtx = arguments.NumCtx,
            NumPredict = arguments.NumPredict,
        }));

        try
        {
            await client.CompleteJsonAsync<Dictionary<string, string>>("Reply with {\"ok\":\"yes\"}", "{\"type\":\"object\"}", cancellationToken);
        }
        catch (LlmException)
        {
            // The warm-up only loads the model. A failure shows up in the timed runs anyway.
        }
    }

    private static async Task<LlmConfigReport> RunConfigAsync(
        HttpClient http,
        IKnowledgeBase knowledgeBase,
        IReadOnlyList<LlmEvalCase> cases,
        LlmConfig config,
        LlmEvalArguments arguments,
        int position,
        int total,
        CancellationToken cancellationToken)
    {
        var client = new OllamaLlmClient(http, Options.Create(new LlmOptions
        {
            Provider = "Ollama",
            Model = config.Model,
            OllamaUrl = arguments.OllamaUrl,
            TimeoutSeconds = arguments.TimeoutSeconds,
            Temperature = config.Temperature,
            TopP = config.TopP,
            NumCtx = arguments.NumCtx,
            NumPredict = arguments.NumPredict,
            PromptVariant = config.Variant,
        }));

        var ruleEngine = new RuleEngine(
        [
            new SqlConcatenationRule(),
            new XssOutputRule(),
            new HardCodedSecretRule(),
            new OnErrorResumeNextRule(),
            new ComObjectRule(),
            new SessionUsageRule(),
            new IncludeRule(),
        ]);
        var evaluator = new LlmEvaluator(ruleEngine, new Bm25Retriever(knowledgeBase, config.Bm25), new RoslynCodeValidator());
        var evalOptions = new LlmEvalOptions(arguments.RunSummary, arguments.RunMigration, config.Variant, config.TopK);

        var runs = new List<LlmRunReport>();
        for (var run = 1; run <= arguments.Runs; run++)
        {
            var results = new List<LlmCaseResult>();
            foreach (var evalCase in cases)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = await evaluator.RunCaseAsync(client, evalCase, evalOptions, cancellationToken);
                results.Add(result);
                Console.Error.WriteLine(ProgressLine(config, position, total, run, arguments, result));
            }

            runs.Add(new LlmRunReport(run, LlmMetrics.ForRun(results), results.Select(LlmCaseReport.From).ToList()));
        }

        // A config that was cut short is not completed, so it is not written.
        cancellationToken.ThrowIfCancellationRequested();
        return LlmConfigReport.From(config, runs);
    }

    private static string ProgressLine(LlmConfig config, int position, int total, int run, LlmEvalArguments arguments, LlmCaseResult result)
    {
        var parts = new List<string>();
        if (arguments.RunSummary)
        {
            parts.Add(result.Summary is { } summary
                ? $"recall {Fixed(summary.FactRecall)} halluc {Fixed(summary.HallucinationRate)}"
                : $"summary error: {Shorten(result.SummaryError)}");
        }

        if (arguments.RunMigration)
        {
            parts.Add(result.Migration is { } migration
                ? $"compile {CompileText(migration.CompiledFirstAttempt, migration.CompiledAfterRepair)} sources {(migration.SourcesValid ? "ok" : "bad")}"
                : $"migration error: {Shorten(result.MigrationError)}");
        }

        var seconds = (result.SummaryDuration + result.MigrationDuration).TotalSeconds;
        var settings = $"{config.Model} T={Number(config.Temperature)} P={Number(config.TopP)} {LlmReport.VariantName(config.Variant)} "
            + $"K={config.TopK} bm25={Number(config.Bm25.K1)}/{Number(config.Bm25.B)}";
        return $"[{position}/{total}] {settings} run {run}/{arguments.Runs} {result.CaseId} {Fixed(seconds)}s {string.Join("; ", parts)}";
    }

    private static string CompileText(bool firstAttempt, bool afterRepair)
    {
        return firstAttempt ? "1st" : afterRepair ? "after repair" : "no";
    }

    private static string Shorten(string? text)
    {
        if (text is null)
        {
            return "";
        }

        var single = text.ReplaceLineEndings(" ");
        return single.Length <= MaxShortTextLength ? single : single[..MaxShortTextLength] + "...";
    }

    private static async Task WriteResultsAsync(LlmEvalReport report, string resultsPath, string jsonPath, string markdownPath)
    {
        // The writes are not cancelled, so a Ctrl+C never leaves half a file.
        Directory.CreateDirectory(resultsPath);
        await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(report, WriteOptions));
        await File.WriteAllTextAsync(markdownPath, LlmReport.Markdown(report.Configs));
    }

    private static HttpClient CreateClient(string url, int timeoutSeconds)
    {
        return new HttpClient
        {
            BaseAddress = new Uri(url.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(timeoutSeconds),
        };
    }

    private static string Fixed(double value) => value.ToString("F2", CultureInfo.InvariantCulture);

    private static string Number(double value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>The reply of GET api/tags.</summary>
    private sealed record TagsReply(List<TagModel>? Models);

    private sealed record TagModel(string? Name);
}
