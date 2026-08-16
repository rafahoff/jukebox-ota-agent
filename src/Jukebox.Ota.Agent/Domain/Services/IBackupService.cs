using Jukebox.Ota.Agent.Domain.ValueObjects;

namespace Jukebox.Ota.Agent.Domain.Services;

/// <summary>Backup pré-update e restauração opcional no rollback.</summary>
public interface IBackupService
{
    Task<string> CreatePreUpdateBackupAsync(OtaAgentConfig config, string version, CancellationToken cancellationToken = default);

    Task RestorePreUpdateBackupAsync(OtaAgentConfig config, string backupDir, CancellationToken cancellationToken = default);

    void CollectGarbage(string backupsDir, int maxFolders);
}
