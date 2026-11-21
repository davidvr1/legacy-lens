namespace LegacyLens.Core.Llm;

/// <summary>The language model could not be reached, returned an HTTP error, or timed out.</summary>
public class LlmException : Exception
{
    public LlmException(string message)
        : base(message)
    {
    }

    public LlmException(string message, Exception inner)
        : base(message, inner)
    {
    }
}

/// <summary>The language model replied, but the reply is not valid JSON for the requested type.</summary>
public sealed class LlmInvalidResponseException : LlmException
{
    public LlmInvalidResponseException(string message)
        : base(message)
    {
    }

    public LlmInvalidResponseException(string message, Exception inner)
        : base(message, inner)
    {
    }
}
