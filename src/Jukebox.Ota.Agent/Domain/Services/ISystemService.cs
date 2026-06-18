namespace Jukebox.Ota.Agent.Domain.Services;

/// <summary>Controlo de serviços systemd (stop/start/is-active).</summary>
public interface ISystemService
{
    Task StopServiceAsync(string serviceName, CancellationToken cancellationToken = default);

    Task StartServiceAsync(string serviceName, CancellationToken cancellationToken = default);

    Task<bool> IsServiceActiveAsync(string serviceName, CancellationToken cancellationToken = default);
}
