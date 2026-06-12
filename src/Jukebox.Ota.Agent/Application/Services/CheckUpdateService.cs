using Jukebox.Ota.Agent.Domain.Repositories;
using Jukebox.Ota.Agent.Domain.Services;
using Jukebox.Ota.Agent.Domain.ValueObjects;
using Jukebox.Ota.Agent.Infrastructure.Config;

namespace Jukebox.Ota.Agent.Application.Services;

public sealed class CheckUpdateService
{
    private readonly JsonConfigLoader _configLoader;
    private readonly IOtaUpdateClient _otaClient;
    private readonly ITelemetryReporter _telemetry;

    public CheckUpdateService(
        JsonConfigLoader configLoader,
        IOtaUpdateClient otaClient,
        ITelemetryReporter telemetry)
    {
        _configLoader = configLoader;
        _otaClient = otaClient;
        _telemetry = telemetry;
    }

    public async Task<int> RunAsync(string configPath, CancellationToken cancellationToken = default)
    {
        try
        {
            var config = _configLoader.Load(configPath);
            var manifest = await _otaClient.CheckAsync(config, cancellationToken);

            if (manifest is null)
            {
                Console.WriteLine("Nenhuma atualização disponível.");
                _telemetry.ReportCheckResult(config.DeviceId, false, null, null);
                return 0;
            }

            var updateAvailable = !string.Equals(
                manifest.Version,
                config.CurrentVersion,
                StringComparison.Ordinal);

            Console.WriteLine(updateAvailable
                ? $"Atualização disponível: {manifest.Version} (atual: {config.CurrentVersion})"
                : $"Versão remota {manifest.Version} coincide com a atual.");

            _telemetry.ReportCheckResult(config.DeviceId, updateAvailable, manifest.Version, null);
            return updateAvailable ? 2 : 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"check falhou: {ex.Message}");
            _telemetry.ReportCheckResult("desconhecido", false, null, ex.Message);
            return 1;
        }
    }
}
