using Jukebox.Ota.Agent.Domain.Entities;
using Jukebox.Ota.Agent.Domain.ValueObjects;

namespace Jukebox.Ota.Agent.Domain.Repositories;

/// <summary>Cliente da API OTA (check de atualização).</summary>
public interface IOtaUpdateClient
{
    Task<UpdateManifest?> CheckAsync(OtaAgentConfig config, CancellationToken cancellationToken = default);
}
