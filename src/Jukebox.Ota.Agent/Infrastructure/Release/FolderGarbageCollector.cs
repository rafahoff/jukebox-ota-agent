namespace Jukebox.Ota.Agent.Infrastructure.Release;

/// <summary>Remove pastas antigas preservando alvos protegidos (symlinks current/previous).</summary>
public static class FolderGarbageCollector
{
    public static void Collect(string parentDir, IReadOnlySet<string> protectedFolderNames, int maxFolders)
    {
        if (!Directory.Exists(parentDir))
        {
            return;
        }

        var folders = Directory.GetDirectories(parentDir)
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrEmpty(name))
            .Cast<string>()
            .OrderByDescending(name => name, StringComparer.Ordinal)
            .ToList();

        var keep = new HashSet<string>(protectedFolderNames, StringComparer.Ordinal);
        foreach (var folder in folders)
        {
            if (keep.Count >= maxFolders)
            {
                break;
            }

            if (!keep.Contains(folder))
            {
                keep.Add(folder);
            }
        }

        foreach (var folder in folders)
        {
            if (keep.Contains(folder))
            {
                continue;
            }

            var fullPath = Path.Combine(parentDir, folder);
            Directory.Delete(fullPath, recursive: true);
        }
    }
}
