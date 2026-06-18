namespace Jukebox.Ota.Agent.Domain.ValueObjects;

/// <summary>Configuração local do agente OTA (arquivo JSON dedicado no MVP).</summary>
public sealed record OtaAgentConfig(
    string DeviceId,
    string Channel,
    string OtaBaseUrl,
    string CurrentVersion,
    string PublicKeyPath,
    string KioskServiceName = "jukeeo_kiosk_flutterpi.service",
    string ReleasesDir = "/opt/jukeeo/releases",
    string CurrentSymlink = "/opt/jukeeo/current",
    string PreviousSymlink = "/opt/jukeeo/previous",
    string BackupsDir = "/opt/jukeeo/backups",
    string HealthUrl = "http://127.0.0.1:8080/api/health",
    string KioskDataDir = "",
    int MaxReleaseFolders = 7
);
