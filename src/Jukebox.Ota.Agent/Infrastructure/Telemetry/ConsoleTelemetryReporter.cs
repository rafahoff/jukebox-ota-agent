using Jukebox.Ota.Agent.Domain.Services;

namespace Jukebox.Ota.Agent.Infrastructure.Telemetry;

/// <summary>Reporter mínimo — stdout estruturado (journald captura via systemd).</summary>
public sealed class ConsoleTelemetryReporter : ITelemetryReporter
{
    public void ReportVersion(string version) =>
        Console.WriteLine($"[telemetry] event=version version={version}");

    public void ReportCheckResult(string deviceId, bool updateAvailable, string? remoteVersion, string? error)
    {
        if (error is not null)
        {
            Console.Error.WriteLine($"[telemetry] event=check device_id={deviceId} success=false error={error}");
            return;
        }

        Console.WriteLine(
            $"[telemetry] event=check device_id={deviceId} update_available={updateAvailable.ToString().ToLowerInvariant()} remote_version={remoteVersion ?? "none"}");
    }

    public void ReportVerifyResult(bool success, string message) =>
        Console.WriteLine($"[telemetry] event=verify success={success.ToString().ToLowerInvariant()} message={message}");
}
