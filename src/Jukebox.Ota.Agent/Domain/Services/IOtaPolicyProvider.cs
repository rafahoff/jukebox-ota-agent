using Jukebox.Ota.Agent.Domain.ValueObjects;

namespace Jukebox.Ota.Agent.Domain.Services;

/// <summary>Fornece a política OTA (SQLite do kiosk ou defaults).</summary>
public interface IOtaPolicyProvider
{
    OtaCheckPolicy GetPolicy(OtaAgentConfig config);
}
