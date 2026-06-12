namespace Jukebox.Ota.Agent.Domain.ValueObjects;

/// <summary>Configuração local do agente OTA (arquivo JSON dedicado no MVP).</summary>
public sealed record OtaAgentConfig(
    string DeviceId,
    string Channel,
    string OtaBaseUrl,
    string CurrentVersion,
    string PublicKeyPath
);
