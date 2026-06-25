using Jukebox.Ota.Agent.Domain.Entities;
using Jukebox.Ota.Agent.Domain.Repositories;
using Jukebox.Ota.Agent.Domain.Services;
using Jukebox.Ota.Agent.Domain.ValueObjects;
using Jukebox.Ota.Agent.Infrastructure.Config;
using Jukebox.Ota.Agent.Infrastructure.Logging;
using Jukebox.Ota.Agent.Infrastructure.Manifest;
using Jukebox.Ota.Agent.Infrastructure.Policy;

namespace Jukebox.Ota.Agent.Application.Services;

public sealed class ApplyUpdateService
{
    private static readonly TimeSpan HealthTimeout = TimeSpan.FromSeconds(90);

    private readonly JsonConfigLoader _configLoader;
    private readonly OtaConfigVersionSync _versionSync;
    private readonly JsonManifestLoader _manifestLoader;
    private readonly IPackageVerifier _packageVerifier;
    private readonly ISystemService _systemService;
    private readonly IReleaseManager _releaseManager;
    private readonly IBackupService _backupService;
    private readonly IHealthChecker _healthChecker;
    private readonly IOtaAckClient _ackClient;
    private readonly IOtaPolicyProvider _policyProvider;
    private readonly IOtaUpdateStatusStore _statusStore;

    public ApplyUpdateService(
        JsonConfigLoader configLoader,
        OtaConfigVersionSync versionSync,
        JsonManifestLoader manifestLoader,
        IPackageVerifier packageVerifier,
        ISystemService systemService,
        IReleaseManager releaseManager,
        IBackupService backupService,
        IHealthChecker healthChecker,
        IOtaAckClient ackClient,
        IOtaPolicyProvider policyProvider,
        IOtaUpdateStatusStore statusStore)
    {
        _configLoader = configLoader;
        _versionSync = versionSync;
        _manifestLoader = manifestLoader;
        _packageVerifier = packageVerifier;
        _systemService = systemService;
        _releaseManager = releaseManager;
        _backupService = backupService;
        _healthChecker = healthChecker;
        _ackClient = ackClient;
        _policyProvider = policyProvider;
        _statusStore = statusStore;
    }

    public async Task<int> RunAsync(
        string configPath,
        string manifestPath,
        string? packagePath,
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        var config = _configLoader.Load(configPath);
        config = _versionSync.ResolveAndSync(configPath, config);
        var manifest = _manifestLoader.Load(manifestPath);
        var versionPrevious = _releaseManager.GetCurrentReleaseVersion(config) ?? config.CurrentVersion;

        WriteStatus(config, status => status with
        {
            Phase = OtaUpdatePhases.Applying,
            CurrentVersion = config.CurrentVersion,
            RemoteVersion = manifest.Version,
            UpdateAvailable = true,
            ErrorMessage = null,
        });

        if (!force)
        {
            var policy = _policyProvider.GetPolicy(config);
            var nowLocal = TimeOnly.FromDateTime(DateTime.Now);
            if (!policy.Enabled)
            {
                Console.Error.WriteLine("Apply recusado: verificação OTA desabilitada (ota_check_enabled=false).");
                FileAgentLogger.LogApply("recusado: verificação OTA desabilitada (ota_check_enabled=false).");
                WriteApplyError(config, "Verificação OTA desabilitada.");
                return 1;
            }

            if (!OtaCheckSchedule.IsWithinWindow(nowLocal, policy.WindowStart, policy.WindowEnd))
            {
                Console.Error.WriteLine(
                    $"Apply recusado: fora da janela de manutenção OTA ({policy.WindowStart:HH\\:mm}–{policy.WindowEnd:HH\\:mm}, agora {nowLocal:HH\\:mm}).");
                FileAgentLogger.LogApply(
                    $"recusado: fora da janela de manutenção OTA ({policy.WindowStart:HH\\:mm}–{policy.WindowEnd:HH\\:mm}, agora {nowLocal:HH\\:mm}).");
                WriteApplyError(config, "Fora da janela de manutenção OTA.");
                return 1;
            }
        }

        if (string.IsNullOrWhiteSpace(packagePath))
        {
            FileAgentLogger.LogApply("falhou: pacote não informado (--package).");
            WriteApplyError(config, "Pacote não informado (--package).");
            await SendAckAsync(config, manifest, versionPrevious, versionPrevious, "error", "download_failed", "Pacote não informado (--package).", cancellationToken);
            return 1;
        }

        var verifyResult = await _packageVerifier.VerifyAsync(manifest, packagePath, config.PublicKeyPath, cancellationToken);
        if (!verifyResult.Success)
        {
            FileAgentLogger.LogApply($"falhou na verificação: {verifyResult.Message}");
            WriteApplyError(config, verifyResult.Message);
            var errorCode = verifyResult.Message.Contains("SHA-256", StringComparison.Ordinal)
                ? "hash_mismatch"
                : verifyResult.Message.Contains("Assinatura", StringComparison.Ordinal)
                    ? "signature_invalid"
                    : "hash_mismatch";
            await SendAckAsync(config, manifest, versionPrevious, versionPrevious, "error", errorCode, verifyResult.Message, cancellationToken);
            return 1;
        }

        var kioskStopped = false;

        try
        {
            Console.WriteLine($"Parando {config.KioskServiceName}...");
            FileAgentLogger.LogApply($"Parando {config.KioskServiceName}...");
            await _systemService.StopServiceAsync(config.KioskServiceName, cancellationToken);
            kioskStopped = true;

            Console.WriteLine("Actualizando symlink previous ← current...");
            FileAgentLogger.LogApply("Actualizando symlink previous ← current...");
            await _releaseManager.PointPreviousToCurrentAsync(config, cancellationToken);

            Console.WriteLine("Criando backup pré-update...");
            FileAgentLogger.LogApply("Criando backup pré-update...");
            await _backupService.CreatePreUpdateBackupAsync(config, manifest.Version, cancellationToken);

            Console.WriteLine($"Extraindo release {manifest.Version}+{manifest.Arch}...");
            FileAgentLogger.LogApply($"Extraindo release {manifest.Version}+{manifest.Arch}...");
            await _releaseManager.ExtractReleaseAsync(config, packagePath, manifest.Version, manifest.Arch, cancellationToken);

            Console.WriteLine("Trocando symlink current...");
            FileAgentLogger.LogApply("Trocando symlink current...");
            await _releaseManager.SwapCurrentToReleaseAsync(config, manifest.Version, manifest.Arch, cancellationToken);

            Console.WriteLine($"Iniciando {config.KioskServiceName}...");
            FileAgentLogger.LogApply($"Iniciando {config.KioskServiceName}...");
            await _systemService.StartServiceAsync(config.KioskServiceName, cancellationToken);

            Console.WriteLine($"Aguardando health ({config.HealthUrl})...");
            FileAgentLogger.LogApply($"Aguardando health ({config.HealthUrl})...");
            var health = await _healthChecker.WaitForHealthyAsync(
                config.KioskServiceName,
                config.HealthUrl,
                manifest.Version,
                HealthTimeout,
                cancellationToken);

            if (!health.Success)
            {
                Console.Error.WriteLine($"Health falhou: {health.Message}");
                FileAgentLogger.LogApply($"Health falhou: {health.Message}");
                WriteApplyError(config, health.Message ?? "Health check falhou.");
                await RollbackAsync(config, cancellationToken);
                var rolledBackVersion = _releaseManager.GetCurrentReleaseVersion(config) ?? versionPrevious;
                await SendAckAsync(config, manifest, versionPrevious, rolledBackVersion, "rolled_back", health.ErrorCode, health.Message, cancellationToken);
                return 1;
            }

            CollectGarbageSafely(config);

            await SendAckAsync(config, manifest, versionPrevious, manifest.Version, "success", null, null, cancellationToken);
            try
            {
                _versionSync.PersistCurrentVersion(configPath, manifest.Version);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"AVISO: apply concluído mas current_version no config não foi gravada: {ex.Message}");
                FileAgentLogger.LogApply($"AVISO: current_version no config não gravada: {ex.Message}");
            }

            config = config with { CurrentVersion = manifest.Version };
            WriteStatus(config, status => status with
            {
                Phase = OtaUpdatePhases.Idle,
                CurrentVersion = manifest.Version,
                RemoteVersion = null,
                UpdateAvailable = false,
                ErrorMessage = null,
            });
            Console.WriteLine($"Apply concluído: {manifest.Version}");
            FileAgentLogger.LogApply($"concluído: {manifest.Version}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"apply falhou: {ex.Message}");
            FileAgentLogger.LogApply($"falhou: {ex.Message}");
            WriteApplyError(config, ex.Message);

            if (kioskStopped)
            {
                try
                {
                    await RollbackAsync(config, cancellationToken);
                    var rolledBackVersion = _releaseManager.GetCurrentReleaseVersion(config) ?? versionPrevious;
                    await SendAckAsync(config, manifest, versionPrevious, rolledBackVersion, "rolled_back", "service_inactive", ex.Message, cancellationToken);
                }
                catch (Exception rollbackEx)
                {
                    await SendAckAsync(config, manifest, versionPrevious, versionPrevious, "error", "rollback_failed", rollbackEx.Message, cancellationToken);
                }
            }
            else
            {
                await SendAckAsync(config, manifest, versionPrevious, versionPrevious, "error", "service_inactive", ex.Message, cancellationToken);
            }

            return 1;
        }
    }

    /// GC após health OK — falha de permissão em backup antigo não deve invalidar o apply.
    private void CollectGarbageSafely(OtaAgentConfig config)
    {
        try
        {
            _releaseManager.CollectGarbage(config);
            _backupService.CollectGarbage(config.BackupsDir, config.MaxReleaseFolders);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"AVISO: GC após apply falhou (apply já validado): {ex.Message}");
            FileAgentLogger.LogApply($"AVISO: GC após apply ignorado: {ex.Message}");
        }
    }

    private async Task RollbackAsync(Domain.ValueObjects.OtaAgentConfig config, CancellationToken cancellationToken)
    {
        Console.WriteLine("Executando rollback current → previous...");
        FileAgentLogger.LogApply("Executando rollback current → previous...");
        await _releaseManager.RollbackCurrentToPreviousAsync(config, cancellationToken);
        await _systemService.StartServiceAsync(config.KioskServiceName, cancellationToken);
    }

    private Task SendAckAsync(
        Domain.ValueObjects.OtaAgentConfig config,
        UpdateManifest manifest,
        string versionPrevious,
        string versionCurrent,
        string result,
        string? errorCode,
        string? errorMessage,
        CancellationToken cancellationToken)
    {
        var payload = new UpdateAckPayload(
            config.DeviceId,
            manifest.App,
            config.Channel,
            manifest.Version,
            versionPrevious,
            versionCurrent,
            result,
            errorCode,
            errorMessage,
            manifest.PackageType,
            manifest.Arch,
            DateTimeOffset.UtcNow);

        return _ackClient.SendAckAsync(config, payload, cancellationToken);
    }

    private void WriteApplyError(OtaAgentConfig config, string message)
    {
        WriteStatus(config, status => status with
        {
            Phase = OtaUpdatePhases.Error,
            CurrentVersion = config.CurrentVersion,
            ErrorMessage = message,
        });
    }

    private void WriteStatus(OtaAgentConfig config, Func<OtaUpdateStatus, OtaUpdateStatus> mutator)
    {
        var current = _statusStore.Read(config);
        _statusStore.Write(config, mutator(current));
    }
}
