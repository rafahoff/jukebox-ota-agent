using Jukebox.Ota.Agent.Domain.ValueObjects;

namespace Jukebox.Ota.Agent.Domain.Services;

/// <summary>Gestão de releases, symlinks current/previous e GC.</summary>
public interface IReleaseManager
{
    string? GetCurrentReleaseVersion(OtaAgentConfig config);

    string? GetPreviousReleaseVersion(OtaAgentConfig config);

    Task PointPreviousToCurrentAsync(OtaAgentConfig config, CancellationToken cancellationToken = default);

    Task ExtractReleaseAsync(
        OtaAgentConfig config,
        string packagePath,
        string version,
        string arch,
        CancellationToken cancellationToken = default);

    Task SwapCurrentToReleaseAsync(OtaAgentConfig config, string version, string arch, CancellationToken cancellationToken = default);

    Task RollbackCurrentToPreviousAsync(OtaAgentConfig config, CancellationToken cancellationToken = default);

    void CollectGarbage(OtaAgentConfig config);
}
