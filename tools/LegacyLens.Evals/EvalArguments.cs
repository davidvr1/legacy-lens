using System.Globalization;

namespace LegacyLens.Evals;

/// <summary>Options of the "retrieval" command. Parsed by hand, so the tool needs no packages.</summary>
internal sealed record EvalArguments(int K, IReadOnlyList<string> Modes, string OllamaUrl, string EmbeddingModel)
{
    public const string Usage = """
        Usage:
          dotnet run --project tools/LegacyLens.Evals -- retrieval [--k 3] [--modes bm25,embedding,hybrid]
                                                     [--ollama-url http://localhost:11434] [--embedding-model nomic-embed-text]
        """;

    private static readonly string[] KnownModes = ["bm25", "embedding", "hybrid"];

    /// <summary>Throws <see cref="ArgumentException"/> with a message when an option is unknown or has a bad value.</summary>
    public static EvalArguments Parse(string[] args)
    {
        var k = 3;
        IReadOnlyList<string> modes = ["bm25"];
        var ollamaUrl = "http://localhost:11434";
        var embeddingModel = "nomic-embed-text";

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
                case "--k":
                    if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out k) || k <= 0)
                    {
                        throw new ArgumentException("--k must be a positive whole number.");
                    }

                    break;
                case "--modes":
                    modes = ParseModes(value);
                    break;
                case "--ollama-url":
                    if (!Uri.TryCreate(value, UriKind.Absolute, out _))
                    {
                        throw new ArgumentException($"--ollama-url is not a valid absolute URL: '{value}'.");
                    }

                    ollamaUrl = value;
                    break;
                case "--embedding-model":
                    embeddingModel = value;
                    break;
                default:
                    throw new ArgumentException($"Unknown option '{name}'.");
            }
        }

        return new EvalArguments(k, modes, ollamaUrl, embeddingModel);
    }

    private static IReadOnlyList<string> ParseModes(string value)
    {
        var modes = value
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(mode => mode.ToLowerInvariant())
            .Distinct()
            .ToList();

        if (modes.Count == 0 || modes.Any(mode => !KnownModes.Contains(mode)))
        {
            throw new ArgumentException("--modes takes a comma-separated list of bm25, embedding and hybrid.");
        }

        return modes;
    }
}
