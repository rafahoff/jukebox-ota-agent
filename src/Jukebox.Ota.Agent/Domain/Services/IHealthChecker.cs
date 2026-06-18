namespace Jukebox.Ota.Agent.Domain.Services;

/// <summary>Verifica saúde do kiosk após apply (systemd + /api/health).</summary>
public interface IHealthChecker
{
    Task<HealthCheckResult> WaitForHealthyAsync(
        string serviceName,
        string healthUrl,
        string expectedAppVersion,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}

public sealed record HealthCheckResult(bool Success, string? ErrorCode, string? Message);
