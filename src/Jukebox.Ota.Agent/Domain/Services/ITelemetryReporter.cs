namespace Jukebox.Ota.Agent.Domain.Services;

/// <summary>Telemetria mínima do agente (journald/log estruturado na infra).</summary>
public interface ITelemetryReporter
{
    void ReportVersion(string version);
    void ReportCheckResult(string deviceId, bool updateAvailable, string? remoteVersion, string? error);
    void ReportCheckSkipped(string deviceId, string reason);
    void ReportVerifyResult(bool success, string message);
}
