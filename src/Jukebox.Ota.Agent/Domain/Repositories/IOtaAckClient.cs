using Jukebox.Ota.Agent.Domain.Entities;
using Jukebox.Ota.Agent.Domain.ValueObjects;

namespace Jukebox.Ota.Agent.Domain.Repositories;

/// <summary>Cliente para confirmação de apply ao servidor OTA.</summary>
public interface IOtaAckClient
{
    Task SendAckAsync(OtaAgentConfig config, UpdateAckPayload payload, CancellationToken cancellationToken = default);
}
