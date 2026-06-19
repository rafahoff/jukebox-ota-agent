using Jukebox.Ota.Agent.Domain.ValueObjects;

namespace Jukebox.Ota.Agent.Domain.Services;

/// <summary>Persiste <c>ota_update_status.json</c> no diretório de dados do kiosk.</summary>
public interface IOtaUpdateStatusStore
{
    OtaUpdateStatus Read(OtaAgentConfig config);

    void Write(OtaAgentConfig config, OtaUpdateStatus status);

    DateTimeOffset? GetCheckedAt(OtaAgentConfig config);

    void SetCheckedAt(OtaAgentConfig config, DateTimeOffset timestamp);
}
