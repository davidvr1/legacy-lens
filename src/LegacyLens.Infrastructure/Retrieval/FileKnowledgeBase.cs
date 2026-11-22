using LegacyLens.Core.Model;
using LegacyLens.Core.Retrieval;
using Microsoft.Extensions.Options;

namespace LegacyLens.Infrastructure.Retrieval;

/// <summary>Reads the top-level *.md files of the knowledge folder once, and keeps them in memory.</summary>
public sealed class FileKnowledgeBase(IOptions<RetrievalOptions> options) : IKnowledgeBase
{
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private volatile IReadOnlyList<KnowledgeChunk>? _chunks;

    public async Task<IReadOnlyList<KnowledgeChunk>> GetChunksAsync(CancellationToken cancellationToken)
    {
        var existing = _chunks;
        if (existing is not null)
        {
            return existing;
        }

        await _loadLock.WaitAsync(cancellationToken);
        try
        {
            var loaded = _chunks ?? await LoadAsync(cancellationToken);
            _chunks = loaded;
            return loaded;
        }
        finally
        {
            _loadLock.Release();
        }
    }

    /// <summary>Throws <see cref="InvalidOperationException"/> when the knowledge folder does not exist.</summary>
    private async Task<IReadOnlyList<KnowledgeChunk>> LoadAsync(CancellationToken cancellationToken)
    {
        var folder = Path.Combine(AppContext.BaseDirectory, options.Value.KnowledgePath);
        if (!Directory.Exists(folder))
        {
            throw new InvalidOperationException($"Knowledge folder not found: '{folder}'.");
        }

        var files = Directory.GetFiles(folder, "*.md", SearchOption.TopDirectoryOnly)
            .OrderBy(file => Path.GetFileNameWithoutExtension(file), StringComparer.Ordinal);

        var chunks = new List<KnowledgeChunk>();
        foreach (var file in files)
        {
            var markdown = await File.ReadAllTextAsync(file, cancellationToken);
            chunks.Add(KnowledgeParser.Parse(Path.GetFileNameWithoutExtension(file), markdown));
        }

        return chunks;
    }
}
