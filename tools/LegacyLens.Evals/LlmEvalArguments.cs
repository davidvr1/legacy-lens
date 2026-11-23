using System.Globalization;
using LegacyLens.Core.Llm;
using LegacyLens.Core.Retrieval;

namespace LegacyLens.Evals;

/// <summary>Options of the "llm" command. Parsed by hand, so the tool needs no packages.</summary>
internal sealed record LlmEvalArguments(
    IReadOnlyList<string> Models,
    IReadOnlyList<double> Temperatures,
    IReadOnlyList<double> TopPs,
    IReadOnlyList<PromptVariant> Variants,
    IReadOnlyList<int> TopKs,
    IReadOnlyList<Bm25Parameters> Bm25s,
    int Runs,
    bool RunSummary,
    bool RunMigration,
    IReadOnlyList<string>? CaseIds,
    string OllamaUrl,
    int NumCtx,
    int NumPredict,
    int TimeoutSeconds,
    string Label)
{
    public const string Usage = """
        Usage:
          dotnet run --project tools/LegacyLens.Evals -- llm [--models qwen2.5-coder:1.5b] [--temperatures 0] [--top-p 1.0]
                                                     [--variants zero-shot,few-shot] [--top-k 3] [--bm25 1.2:0.75] [--runs 3]
                                                     [--tasks summary,migration] [--cases id1,id2] [--ollama-url http://localhost:11434]
                                                     [--num-ctx 8192] [--num-predict 2048] [--timeout 300] [--label grid]
        """;

    /// <summary>Throws <see cref="ArgumentException"/> with a message when an option is unknown or has a bad value.</summary>
    public static LlmEvalArguments Parse(string[] args)
    {
        IReadOnlyList<string> models = ["qwen2.5-coder:1.5b"];
        IReadOnlyList<double> temperatures = [0];
        IReadOnlyList<double> topPs = [1.0];
        IReadOnlyList<PromptVariant> variants = [PromptVariant.ZeroShot];
        IReadOnlyList<int> topKs = [3];
        IReadOnlyList<Bm25Parameters> bm25s = [new Bm25Parameters(1.2, 0.75)];
        var runs = 3;
        var runSummary = true;
        var runMigration = true;
        IReadOnlyList<string>? caseIds = null;
        var ollamaUrl = "http://localhost:11434";
        var numCtx = 8192;
        var numPredict = 2048;
        var timeoutSeconds = 300;
        var label = "grid";

        for (var i = 0; i < args.Length; i++)
        {
            var name = args[i];
            if (i + 1 >= args.Length)
            {
                throw new ArgumentException($"Missing value for {name}.");
            }

            var value = args[++i];
            switch (name)
            {
                case "--models":
                    models = SplitList(name, value);
                    break;
                case "--temperatures":
                    temperatures = ParseDoubles(name, value, number => number >= 0, "that are 0 or more");
                    break;
                case "--top-p":
                    topPs = ParseDoubles(name, value, number => number > 0 && number <= 1, "between 0 (exclusive) and 1");
                    break;
                case "--variants":
                    variants = SplitList(name, value).Select(ParseVariant).Distinct().ToList();
                    break;
                case "--top-k":
                    topKs = ParsePositiveInts(name, value);
                    break;
                case "--bm25":
                    bm25s = ParseBm25(name, value);
                    break;
                case "--runs":
                    runs = ParsePositiveInt(name, value);
                    break;
                case "--tasks":
                    (runSummary, runMigration) = ParseTasks(name, value);
                    break;
                case "--cases":
                    caseIds = SplitList(name, value);
                    break;
                case "--ollama-url":
                    if (!Uri.TryCreate(value, UriKind.Absolute, out _))
                    {
                        throw new ArgumentException($"--ollama-url is not a valid absolute URL: '{value}'.");
                    }

                    ollamaUrl = value;
                    break;
                case "--num-predict":
                    numPredict = ParsePositiveInt(name, value);
                    break;
                case "--num-ctx":
                    numCtx = ParsePositiveInt(name, value);
                    break;
                case "--timeout":
                    timeoutSeconds = ParsePositiveInt(name, value);
                    break;
                case "--label":
                    if (value.Length == 0 || !value.All(c => char.IsAsciiLetterOrDigit(c) || c == '-'))
                    {
                        throw new ArgumentException("--label may only contain letters, digits and '-'.");
                    }

                    label = value;
                    break;
                default:
                    throw new ArgumentException($"Unknown option '{name}'.");
            }
        }

        return new LlmEvalArguments(
            models, temperatures, topPs, variants, topKs, bm25s, runs, runSummary, runMigration, caseIds, ollamaUrl, numCtx, numPredict, timeoutSeconds, label);
    }

    private static IReadOnlyList<string> SplitList(string option, string value)
    {
        var items = value
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Distinct()
            .ToList();

        if (items.Count == 0)
        {
            throw new ArgumentException($"{option} needs at least one value.");
        }

        return items;
    }

    private static IReadOnlyList<double> ParseDoubles(string option, string value, Func<double, bool> isValid, string rule)
    {
        return SplitList(option, value)
            .Select(item => TryParseDouble(item, out var number) && isValid(number)
                ? number
                : throw new ArgumentException($"{option} must be a comma-separated list of numbers {rule}."))
            .Distinct()
            .ToList();
    }

    private static IReadOnlyList<int> ParsePositiveInts(string option, string value)
    {
        return SplitList(option, value)
            .Select(item => int.TryParse(item, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number > 0
                ? number
                : throw new ArgumentException($"{option} must be a comma-separated list of positive whole numbers."))
            .Distinct()
            .ToList();
    }

    private static int ParsePositiveInt(string option, string value)
    {
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number <= 0)
        {
            throw new ArgumentException($"{option} must be a positive whole number.");
        }

        return number;
    }

    private static IReadOnlyList<Bm25Parameters> ParseBm25(string option, string value)
    {
        return SplitList(option, value)
            .Select(item =>
            {
                var parts = item.Split(':');
                if (parts.Length != 2
                    || !TryParseDouble(parts[0], out var k1)
                    || !TryParseDouble(parts[1], out var b)
                    || k1 < 0
                    || b < 0
                    || b > 1)
                {
                    throw new ArgumentException($"{option} takes a comma-separated list of k1:b pairs, for example 1.2:0.75.");
                }

                return new Bm25Parameters(k1, b);
            })
            .Distinct()
            .ToList();
    }

    private static (bool Summary, bool Migration) ParseTasks(string option, string value)
    {
        var tasks = SplitList(option, value).Select(task => task.ToLowerInvariant()).ToList();
        if (tasks.Any(task => task is not ("summary" or "migration")))
        {
            throw new ArgumentException($"{option} takes a comma-separated list of summary and migration.");
        }

        return (tasks.Contains("summary"), tasks.Contains("migration"));
    }

    private static PromptVariant ParseVariant(string name)
    {
        return name.ToLowerInvariant() switch
        {
            "zero-shot" => PromptVariant.ZeroShot,
            "few-shot" => PromptVariant.FewShot,
            _ => throw new ArgumentException("--variants takes a comma-separated list of zero-shot and few-shot."),
        };
    }

    private static bool TryParseDouble(string text, out double number)
    {
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number) && double.IsFinite(number);
    }
}
