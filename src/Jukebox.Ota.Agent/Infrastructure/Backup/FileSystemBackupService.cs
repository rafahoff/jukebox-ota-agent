using Jukebox.Ota.Agent.Domain.Services;
using Jukebox.Ota.Agent.Domain.ValueObjects;
using Jukebox.Ota.Agent.Infrastructure.Release;

namespace Jukebox.Ota.Agent.Infrastructure.Backup;

public sealed class FileSystemBackupService : IBackupService
{
    private static readonly string[] KioskDatabaseFiles =
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

        if (config.BackupDataFiles is { Count: > 0 })
        {
            foreach (var fileName in config.BackupDataFiles)
            {
                CopyIfExists(Path.Combine(dataDir, fileName), Path.Combine(backupDir, fileName));
            }

            return Task.FromResult(backupDir);
        }

        var hasDb = KioskDatabaseFiles.Any(fileName => File.Exists(Path.Combine(dataDir, fileName)));
        if (!hasDb && !File.Exists(Path.Combine(dataDir, "shared_preferences.json")))
        {
            return Task.FromResult(backupDir);
        }

        foreach (var fileName in KioskDatabaseFiles)
        {
            CopyIfExists(Path.Combine(dataDir, fileName), Path.Combine(backupDir, fileName));
        }

        CopyIfExists(
            Path.Combine(dataDir, "shared_preferences.json"),
            Path.Combine(backupDir, "shared_preferences.json"));

        return Task.FromResult(backupDir);
    }

    public Task RestorePreUpdateBackupAsync(
        OtaAgentConfig config,
        string backupDir,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!Directory.Exists(backupDir))
        {
            return Task.CompletedTask;
        }

        var dataDir = ResolveKioskDataDir(config);
        Directory.CreateDirectory(dataDir);

        foreach (var file in Directory.EnumerateFiles(backupDir))
        {
            var fileName = Path.GetFileName(file);
            CopyIfExists(file, Path.Combine(dataDir, fileName));
        }

        return Task.CompletedTask;
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
