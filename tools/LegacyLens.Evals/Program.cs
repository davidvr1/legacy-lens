using LegacyLens.Evals;

// Usage: dotnet run --project tools/LegacyLens.Evals -- rules|retrieval|llm [options]

const string usage = """
    Usage:
      dotnet run --project tools/LegacyLens.Evals -- rules
      dotnet run --project tools/LegacyLens.Evals -- retrieval [--k 3] [--modes bm25,embedding,hybrid]
                                                 [--ollama-url http://localhost:11434] [--embedding-model nomic-embed-text]
      dotnet run --project tools/LegacyLens.Evals -- llm [--models qwen2.5-coder:1.5b] [--temperatures 0] [--top-p 1.0]
                                                 [--variants zero-shot,few-shot] [--top-k 3] [--bm25 1.2:0.75] [--runs 3]
                                                 [--tasks summary,migration] [--cases id1,id2] [--ollama-url http://localhost:11434]
                                                 [--num-ctx 8192] [--timeout 300] [--label grid]
    """;

if (args.Length == 0)
{
    Console.Error.WriteLine(usage);
    return 1;
}

switch (args[0])
{
    case "rules":
        if (args.Length > 1)
        {
            Console.Error.WriteLine("The rules command takes no options.");
            Console.Error.WriteLine(usage);
            return 1;
        }

        return await RulesCommand.RunAsync(CancellationToken.None);
    case "retrieval":
        return await RetrievalAsync(args[1..]);
    case "llm":
        return await LlmAsync(args[1..]);
    default:
        Console.Error.WriteLine(usage);
        return 1;
}

static async Task<int> RetrievalAsync(string[] options)
{
    EvalArguments arguments;
    try
    {
        arguments = EvalArguments.Parse(options);
    }
    catch (ArgumentException ex)
    {
        Console.Error.WriteLine(ex.Message);
        Console.Error.WriteLine(EvalArguments.Usage);
        return 1;
    }

    return await RetrievalCommand.RunAsync(arguments, CancellationToken.None);
}

static async Task<int> LlmAsync(string[] options)
{
    LlmEvalArguments arguments;
    try
    {
        arguments = LlmEvalArguments.Parse(options);
    }
    catch (ArgumentException ex)
    {
        Console.Error.WriteLine(ex.Message);
        Console.Error.WriteLine(LlmEvalArguments.Usage);
        return 1;
    }

    return await LlmCommand.RunAsync(arguments, CancellationToken.None);
}
