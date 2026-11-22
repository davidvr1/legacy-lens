using LegacyLens.Evals;

// Usage: dotnet run --project tools/LegacyLens.Evals -- retrieval [--k 3] [--modes bm25,embedding,hybrid] [--ollama-url URL] [--embedding-model NAME]

if (args.Length == 0 || !string.Equals(args[0], "retrieval", StringComparison.Ordinal))
{
    Console.Error.WriteLine(EvalArguments.Usage);
    return 1;
}

EvalArguments arguments;
try
{
    arguments = EvalArguments.Parse(args[1..]);
}
catch (ArgumentException ex)
{
    Console.Error.WriteLine(ex.Message);
    Console.Error.WriteLine(EvalArguments.Usage);
    return 1;
}

return await RetrievalCommand.RunAsync(arguments, CancellationToken.None);
