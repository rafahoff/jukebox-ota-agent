namespace Jukebox.Ota.Agent.Domain.ValueObjects;

/// <summary>Estado OTA partilhado com o kiosk (schema v1).</summary>
public sealed record OtaUpdateStatus(
    int SchemaVersion = 1,
    string Phase = OtaUpdatePhases.Idle,
    long? CheckedAtMs = null,
    string CurrentVersion = "0.0.0",
    string? RemoteVersion = null,
    bool UpdateAvailable = false,
    string? ErrorMessage = null);

/// <summary>Fases válidas em <c>ota_update_status.json</c> (ADR 0001).</summary>
public static class OtaUpdatePhases
{
    public const string Idle = "idle";
    public const string Checking = "checking";
    public const string UpdateAvailable = "update_available";
    public const string Downloading = "downloading";
    public const string Applying = "applying";
    public const string Error = "error";
}
