using Jukebox.Ota.Agent.Domain.Repositories;
using Jukebox.Ota.Agent.Domain.Services;
using Jukebox.Ota.Agent.Infrastructure.Config;
using Jukebox.Ota.Agent.Infrastructure.ExternalServices;
using Jukebox.Ota.Agent.Infrastructure.Logging;
using Jukebox.Ota.Agent.Infrastructure.Policy;

namespace Jukebox.Ota.Agent.Application.Services;

public sealed class CheckUpdateService
{
    private readonly JsonConfigLoader _configLoader;
    private readonly IOtaUpdateClient _otaClient;
    private readonly ITelemetryReporter _telemetry;
    private readonly IOtaPolicyProvider _policyProvider;
    private readonly IOtaCheckStateStore _stateStore;

    public CheckUpdateService(
        JsonConfigLoader configLoader,
        IOtaUpdateClient otaClient,
        ITelemetryReporter telemetry,
        IOtaPolicyProvider policyProvider,
        IOtaCheckStateStore stateStore)
    {
        _configLoader = configLoader;
        _otaClient = otaClient;
        _telemetry = telemetry;
        _policyProvider = policyProvider;
        _stateStore = stateStore;
    }

    public async Task<int> RunAsync(string configPath, CancellationToken cancellationToken = default)
    {
        Domain.ValueObjects.OtaAgentConfig? config = null;
        try
        {
            config = _configLoader.Load(configPath);
            var policy = _policyProvider.GetPolicy(config);

            if (!policy.Enabled)
            {
                Console.WriteLine("Check ignorado: verificação OTA desabilitada (ota_check_enabled=false).");
                _telemetry.ReportCheckSkipped(config.DeviceId, "disabled");
                return 0;
            }

            var nowLocal = TimeOnly.FromDateTime(DateTime.Now);
            if (!OtaCheckSchedule.IsWithinWindow(nowLocal, policy.WindowStart, policy.WindowEnd))
            {
                Console.WriteLine(
                    $"Check ignorado: fora da janela horária ({policy.WindowStart:HH\\:mm}–{policy.WindowEnd:HH\\:mm}, agora {nowLocal:HH\\:mm}).");
                _telemetry.ReportCheckSkipped(config.DeviceId, "outside_window");
                return 0;
            }

            var lastCheck = _stateStore.GetLastCheckAt(config.StateDirectory);
            if (lastCheck.HasValue)
            {
                var elapsed = DateTimeOffset.UtcNow - lastCheck.Value;
                var interval = TimeSpan.FromMinutes(policy.IntervalMinutes);
                if (elapsed < interval)
                {
                    Console.WriteLine(
                        $"Check ignorado: intervalo mínimo não atingido ({policy.IntervalMinutes} min; última verificação há {elapsed.TotalMinutes:F0} min).");
                    _telemetry.ReportCheckSkipped(config.DeviceId, "interval_not_elapsed");
                    return 0;
                }
            }

            var manifest = await _otaClient.CheckAsync(config, cancellationToken);

            if (manifest is null)
            {
                Console.WriteLine("Nenhuma atualização disponível.");
                FileAgentLogger.LogCheck("Nenhuma atualização disponível.");
                _telemetry.ReportCheckResult(config.DeviceId, false, null, null);
                _stateStore.SetLastCheckAt(config.StateDirectory, DateTimeOffset.UtcNow);
                return 0;
            }

            var updateAvailable = !string.Equals(
                manifest.Version,
                config.CurrentVersion,
                StringComparison.Ordinal);

            Console.WriteLine(updateAvailable
                ? $"Atualização disponível: {manifest.Version} (atual: {config.CurrentVersion})"
                : $"Versão remota {manifest.Version} coincide com a atual.");

            FileAgentLogger.LogCheck(updateAvailable
                ? $"Atualização disponível: {manifest.Version} (atual: {config.CurrentVersion})"
                : $"Versão remota {manifest.Version} coincide com a atual.");

            _telemetry.ReportCheckResult(config.DeviceId, updateAvailable, manifest.Version, null);
            _stateStore.SetLastCheckAt(config.StateDirectory, DateTimeOffset.UtcNow);
            return updateAvailable ? 2 : 0;
        }
        catch (Exception ex)
        {
            var deviceId = config?.DeviceId ?? "desconhecido";
            Console.Error.WriteLine($"check falhou: {ex.Message}");
            var checkEndpoint = config is not null
                ? HttpOtaUpdateClient.DescribeCheckEndpoint(config)
                : null;
            FileAgentLogger.LogCheckUrlFailure(
                deviceId,
                config?.OtaBaseUrl,
                checkEndpoint,
                ex);
            _telemetry.ReportCheckResult(deviceId, false, null, ex.Message);
            return 1;
        }
    }
}
