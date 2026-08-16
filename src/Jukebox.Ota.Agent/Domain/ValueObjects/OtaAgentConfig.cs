namespace Jukebox.Ota.Agent.Domain.ValueObjects;

/// <summary>Configuração local do agente OTA (arquivo JSON dedicado no MVP).</summary>
public sealed record OtaAgentConfig(
    string DeviceId,
    string Channel,
    string OtaBaseUrl,
    string CurrentVersion,
    string PublicKeyPath,
    string App = OtaPackageNaming.DefaultApp,
    string Arch = "aarch64",
    string KioskServiceName = "jukeeo_kiosk_flutterpi.service",
    IReadOnlyList<string>? StopServices = null,
    IReadOnlyList<string>? StartServices = null,
    IReadOnlyList<string>? StartTimers = null,
    string ReleasesDir = "/opt/jukeeo/releases",
    string CurrentSymlink = "/opt/jukeeo/current",
    string PreviousSymlink = "/opt/jukeeo/previous",
    string? InstallRootSymlink = null,
    string BackupsDir = "/opt/jukeeo/backups",
    string HealthUrl = "http://127.0.0.1:8080/api/health",
    int HealthTimeoutSeconds = 90,
    string KioskDataDir = "",
    IReadOnlyList<string>? BackupDataFiles = null,
    string? PostInstallScript = null,
    int MaxReleaseFolders = 7,
    string StateDirectory = "/var/lib/jukebox-ota",
    bool? OtaCheckEnabled = null,
    int? OtaCheckIntervalMinutes = null,
    string? OtaCheckWindowStart = null,
    string? OtaCheckWindowEnd = null)
{
    public IReadOnlyList<string> ResolveStopServices() =>
        StopServices is { Count: > 0 }
            ? StopServices
            : [KioskServiceName];

    public IReadOnlyList<string> ResolveStartServices() =>
        StartServices is { Count: > 0 }
            ? StartServices
            : [KioskServiceName];

    public bool UsesMultiServiceFlow() =>
        StopServices is { Count: > 0 } || StartServices is { Count: > 0 };

    public string PrimaryHealthServiceName() =>
        ResolveStartServices().LastOrDefault() ?? KioskServiceName;
}
