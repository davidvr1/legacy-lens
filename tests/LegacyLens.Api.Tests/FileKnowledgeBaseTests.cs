using LegacyLens.Infrastructure.Retrieval;
using Microsoft.Extensions.Options;

namespace LegacyLens.Api.Tests;

public sealed class FileKnowledgeBaseTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "legacylens-kb-" + Guid.NewGuid().ToString("N"));

    public FileKnowledgeBaseTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private FileKnowledgeBase Create(string? path = null)
        => new(Options.Create(new RetrievalOptions { KnowledgePath = path ?? _folder }));

    [Fact]
    public async Task Reads_only_md_files_with_ids_titles_and_content_ordered_by_id()
    {
        await File.WriteAllTextAsync(Path.Combine(_folder, "b-note.md"), "# Bravo\nbody b");
        await File.WriteAllTextAsync(Path.Combine(_folder, "a-note.md"), "# Alpha\nbody a");
        await File.WriteAllTextAsync(Path.Combine(_folder, "no-title.md"), "just text");
        await File.WriteAllTextAsync(Path.Combine(_folder, "ignored.txt"), "# Ignored");
        await File.WriteAllTextAsync(Path.Combine(_folder, "README.markdown"), "# Ignored too");
        Directory.CreateDirectory(Path.Combine(_folder, "sub"));
        await File.WriteAllTextAsync(Path.Combine(_folder, "sub", "nested.md"), "# Nested");

        var chunks = await Create().GetChunksAsync(CancellationToken.None);

        Assert.Equal(["a-note", "b-note", "no-title"], chunks.Select(c => c.Id));
        Assert.Equal(["Alpha", "Bravo", "no-title"], chunks.Select(c => c.Title));
        Assert.Equal("# Alpha\nbody a", chunks[0].Content);
    }

    [Fact]
    public async Task Ordering_is_ordinal_by_id()
    {
        foreach (var name in new[] { "b", "C", "a" })
        {
            await File.WriteAllTextAsync(Path.Combine(_folder, name + ".md"), "# " + name);
        }

        var chunks = await Create().GetChunksAsync(CancellationToken.None);

        Assert.Equal(["C", "a", "b"], chunks.Select(c => c.Id));
    }

    [Fact]
    public async Task Empty_folder_gives_empty_list()
    {
        Assert.Empty(await Create().GetChunksAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Missing_folder_throws_InvalidOperationException_with_full_path()
    {
        var missing = Path.Combine(_folder, "does-not-exist");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Create(missing).GetChunksAsync(CancellationToken.None));

        Assert.Contains(missing, ex.Message);
    }

    [Fact]
    public async Task Relative_path_is_resolved_against_base_directory()
    {
        var name = "kb-rel-" + Guid.NewGuid().ToString("N");
        var full = Path.Combine(AppContext.BaseDirectory, name);
        Directory.CreateDirectory(full);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(full, "x.md"), "# X");
            var chunks = await Create(name).GetChunksAsync(CancellationToken.None);
            Assert.Equal(["x"], chunks.Select(c => c.Id));

            var missing = await Assert.ThrowsAsync<InvalidOperationException>(() => Create(name + "-nope").GetChunksAsync(CancellationToken.None));
            Assert.Contains(Path.Combine(AppContext.BaseDirectory, name + "-nope"), missing.Message);
        }
        finally
        {
            Directory.Delete(full, recursive: true);
        }
    }

    [Fact]
    public async Task Result_is_cached_after_first_read()
    {
        await File.WriteAllTextAsync(Path.Combine(_folder, "one.md"), "# One");
        var kb = Create();

        var first = await kb.GetChunksAsync(CancellationToken.None);
        await File.WriteAllTextAsync(Path.Combine(_folder, "two.md"), "# Two");
        var second = await kb.GetChunksAsync(CancellationToken.None);

        Assert.Same(first, second);
        Assert.Single(second);
    }

    [Fact]
    public async Task Api_output_contains_the_12_knowledge_files_with_default_options()
    {
        var kb = new FileKnowledgeBase(Options.Create(new RetrievalOptions()));

        var chunks = await kb.GetChunksAsync(CancellationToken.None);

        Assert.Equal(12, chunks.Count);
        Assert.Contains(chunks, c => c.Id == "session-state");
        Assert.All(chunks, c => Assert.False(string.IsNullOrWhiteSpace(c.Content)));
    }
}
