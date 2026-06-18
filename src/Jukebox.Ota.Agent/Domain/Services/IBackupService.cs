using Jukebox.Ota.Agent.Domain.ValueObjects;

namespace Jukebox.Ota.Agent.Domain.Services;

/// <summary>Backup pré-update de SQLite e shared_preferences do kiosk.</summary>
public interface IBackupService
{
    Task<string> CreatePreUpdateBackupAsync(OtaAgentConfig config, string version, CancellationToken cancellationToken = default);

    void CollectGarbage(string backupsDir, int maxFolders);
}
