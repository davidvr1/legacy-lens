namespace LegacyLens.Core.Llm;

/// <summary>How a prompt is written: with no example, or with one worked example before the real file.</summary>
public enum PromptVariant
{
    ZeroShot,
    FewShot
}
