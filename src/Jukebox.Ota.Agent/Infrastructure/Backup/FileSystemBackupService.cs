using Jukebox.Ota.Agent.Domain.Services;
using Jukebox.Ota.Agent.Domain.ValueObjects;
using Jukebox.Ota.Agent.Infrastructure.Release;

namespace Jukebox.Ota.Agent.Infrastructure.Backup;

public sealed class FileSystemBackupService : IBackupService
{
    private static readonly string[] DatabaseFiles =
    [
        "jukebox_library.db",
        "jukebox_library.db-wal",
        "jukebox_library.db-shm",
    ];

    public Task<string> CreatePreUpdateBackupAsync(
        OtaAgentConfig config,
        string version,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var backupDir = Path.Combine(config.BackupsDir, $"pre-{version}-{timestamp}");
        Directory.CreateDirectory(backupDir);

        var dataDir = ResolveKioskDataDir(config);
        if (!Directory.Exists(dataDir))
        {
            return Task.FromResult(backupDir);
        }

        var hasDb = DatabaseFiles.Any(fileName => File.Exists(Path.Combine(dataDir, fileName)));
        if (!hasDb && !File.Exists(Path.Combine(dataDir, "shared_preferences.json")))
        {
            // Bootstrap: kiosk ainda sem SQLite/prefs — backup vazio.
            return Task.FromResult(backupDir);
        }

        foreach (var fileName in DatabaseFiles)
        {
            CopyIfExists(Path.Combine(dataDir, fileName), Path.Combine(backupDir, fileName));
        }

        CopyIfExists(
            Path.Combine(dataDir, "shared_preferences.json"),
            Path.Combine(backupDir, "shared_preferences.json"));

        return Task.FromResult(backupDir);
    }

    public void CollectGarbage(string backupsDir, int maxFolders)
    {
        try
        {
            FolderGarbageCollector.Collect(backupsDir, new HashSet<string>(StringComparer.Ordinal), maxFolders);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"AVISO: GC de backups falhou: {ex.Message}");
        }
    }

    public static string ResolveKioskDataDir(OtaAgentConfig config)
    {
        if (!string.IsNullOrWhiteSpace(config.KioskDataDir))
        {
            return ExpandHome(config.KioskDataDir);
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".local", "share", "com.jukeeo.kiosk");
    }

    private static string ExpandHome(string path)
    {
        if (path.StartsWith("~/", StringComparison.Ordinal))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, path[2..]);
        }

        return path;
    }

    private static void CopyIfExists(string source, string dest)
    {
        if (!File.Exists(source))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        File.Copy(source, dest, overwrite: true);
    }
}
