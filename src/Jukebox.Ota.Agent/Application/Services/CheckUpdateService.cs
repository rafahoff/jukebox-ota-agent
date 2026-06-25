using Jukebox.Ota.Agent.Domain.Entities;
using Jukebox.Ota.Agent.Domain.Repositories;
using Jukebox.Ota.Agent.Domain.Services;
using Jukebox.Ota.Agent.Domain.ValueObjects;
using Jukebox.Ota.Agent.Infrastructure.Config;
using Jukebox.Ota.Agent.Infrastructure.ExternalServices;
using Jukebox.Ota.Agent.Infrastructure.Logging;
using Jukebox.Ota.Agent.Infrastructure.Policy;

namespace Jukebox.Ota.Agent.Application.Services;

public sealed class CheckUpdateService
{
    private readonly JsonConfigLoader _configLoader;
    private readonly OtaConfigVersionSync _versionSync;
    private readonly IOtaUpdateClient _otaClient;
    private readonly ITelemetryReporter _telemetry;
    private readonly IOtaPolicyProvider _policyProvider;
    private readonly IOtaUpdateStatusStore _statusStore;

    public CheckUpdateService(
        JsonConfigLoader configLoader,
        OtaConfigVersionSync versionSync,
        IOtaUpdateClient otaClient,
        ITelemetryReporter telemetry,
        IOtaPolicyProvider policyProvider,
        IOtaUpdateStatusStore statusStore)
    {
        _configLoader = configLoader;
        _versionSync = versionSync;
        _otaClient = otaClient;
        _telemetry = telemetry;
        _policyProvider = policyProvider;
        _statusStore = statusStore;
    }

    public async Task<int> RunAsync(
        string configPath,
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        var outcome = await ExecuteAsync(configPath, force, cancellationToken);
        return outcome.ExitCode;
    }

    public async Task<CheckUpdateOutcome> ExecuteAsync(
        string configPath,
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        OtaAgentConfig? config = null;

        try
        {
            config = _configLoader.Load(configPath);
            config = _versionSync.ResolveAndSync(configPath, config);
            WriteStatus(config, status => status with
            {
                Phase = OtaUpdatePhases.Checking,
                CurrentVersion = config.CurrentVersion,
                ErrorMessage = null,
            });

            if (!force)
            {
                var policy = _policyProvider.GetPolicy(config);

                if (!policy.Enabled)
                {
                    Console.WriteLine("Check ignorado: verificação OTA desabilitada (ota_check_enabled=false).");
                    _telemetry.ReportCheckSkipped(config.DeviceId, "disabled");
                    WriteSkippedStatus(config, "Verificação OTA desabilitada.");
                    return new CheckUpdateOutcome(0, config, null, false, "disabled", null);
                }

                var nowLocal = TimeOnly.FromDateTime(DateTime.Now);
                if (!OtaCheckSchedule.IsWithinWindow(nowLocal, policy.WindowStart, policy.WindowEnd))
                {
                    Console.WriteLine(
                        $"Check ignorado: fora da janela horária ({policy.WindowStart:HH\\:mm}–{policy.WindowEnd:HH\\:mm}, agora {nowLocal:HH\\:mm}).");
                    _telemetry.ReportCheckSkipped(config.DeviceId, "outside_window");
                    WriteSkippedStatus(config, "Fora da janela horária de verificação OTA.");
                    return new CheckUpdateOutcome(0, config, null, false, "outside_window", null);
                }

                var lastCheck = _statusStore.GetCheckedAt(config);
                if (lastCheck.HasValue)
                {
                    var elapsed = DateTimeOffset.UtcNow - lastCheck.Value;
                    var interval = TimeSpan.FromMinutes(policy.IntervalMinutes);
                    if (elapsed < interval)
                    {
                        Console.WriteLine(
                            $"Check ignorado: intervalo mínimo não atingido ({policy.IntervalMinutes} min; última verificação há {elapsed.TotalMinutes:F0} min).");
                        _telemetry.ReportCheckSkipped(config.DeviceId, "interval_not_elapsed");
                        WriteSkippedStatus(config, "Intervalo mínimo de verificação OTA não atingido.");
                        return new CheckUpdateOutcome(0, config, null, false, "interval_not_elapsed", null);
                    }
                }
            }

            var manifest = await _otaClient.CheckAsync(config, cancellationToken);
            var checkedAt = DateTimeOffset.UtcNow;

            if (manifest is null)
            {
                Console.WriteLine("Nenhuma atualização disponível.");
                FileAgentLogger.LogCheck("Nenhuma atualização disponível.");
                _telemetry.ReportCheckResult(config.DeviceId, false, null, null);
                WriteStatus(config, status => status with
                {
                    Phase = OtaUpdatePhases.Idle,
                    CheckedAtMs = checkedAt.ToUnixTimeMilliseconds(),
                    CurrentVersion = config.CurrentVersion,
                    RemoteVersion = null,
                    UpdateAvailable = false,
                    ErrorMessage = null,
                });
                return new CheckUpdateOutcome(0, config, null, false, null, null);
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
            WriteStatus(config, status => status with
            {
                Phase = updateAvailable ? OtaUpdatePhases.UpdateAvailable : OtaUpdatePhases.Idle,
                CheckedAtMs = checkedAt.ToUnixTimeMilliseconds(),
                CurrentVersion = config.CurrentVersion,
                RemoteVersion = manifest.Version,
                UpdateAvailable = updateAvailable,
                ErrorMessage = null,
            });

            return new CheckUpdateOutcome(updateAvailable ? 2 : 0, config, manifest, updateAvailable, null, null);
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

            if (config is not null)
            {
                WriteStatus(config, status => status with
                {
                    Phase = OtaUpdatePhases.Error,
                    CurrentVersion = config.CurrentVersion,
                    ErrorMessage = ex.Message,
                });
            }

            return new CheckUpdateOutcome(1, config, null, false, null, ex.Message);
        }
    }

    private void WriteSkippedStatus(OtaAgentConfig config, string _)
    {
        var current = _statusStore.Read(config);
        WriteStatus(config, current with
        {
            Phase = OtaUpdatePhases.Idle,
            CurrentVersion = config.CurrentVersion,
            ErrorMessage = null,
        });
    }

    private void WriteStatus(OtaAgentConfig config, Func<OtaUpdateStatus, OtaUpdateStatus> mutator)
    {
        var current = _statusStore.Read(config);
        _statusStore.Write(config, mutator(current));
    }

    private void WriteStatus(OtaAgentConfig config, OtaUpdateStatus status) =>
        _statusStore.Write(config, status);
}
