namespace LegacyLens.Evals;

/// <summary>Finds the repository root, the folder that holds LegacyLens.sln.</summary>
internal static class RepoRoot
{
    /// <summary>Walks up from <paramref name="start"/>. Returns null when no parent holds LegacyLens.sln.</summary>
    public static string? Find(string start)
    {
        for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "LegacyLens.sln")))
            {
                return dir.FullName;
            }
        }

        return null;
    }
}
