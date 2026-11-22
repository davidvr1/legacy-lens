namespace LegacyLens.Infrastructure.Retrieval;

/// <summary>Settings for retrieval, read from the "Retrieval" section of appsettings.json.</summary>
public sealed class RetrievalOptions
{
    public const string SectionName = "Retrieval";

    /// <summary>"Bm25", "Embedding" or "Hybrid".</summary>
    public string Mode { get; set; } = "Bm25";

    /// <summary>Folder with the knowledge notes. A relative path is resolved against the application base directory.</summary>
    public string KnowledgePath { get; set; } = "knowledge";

    public int TopK { get; set; } = 3;

    public double K1 { get; set; } = 1.2;

    public double B { get; set; } = 0.75;

    public string EmbeddingModel { get; set; } = "nomic-embed-text";
}
