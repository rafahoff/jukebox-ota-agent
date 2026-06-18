using Jukebox.Ota.Agent.Domain.Entities;
using Jukebox.Ota.Agent.Domain.Repositories;
using Jukebox.Ota.Agent.Domain.Services;
using Jukebox.Ota.Agent.Infrastructure.Config;
using Jukebox.Ota.Agent.Infrastructure.Manifest;

namespace Jukebox.Ota.Agent.Application.Services;

public sealed class ApplyUpdateService
{
    private static readonly TimeSpan HealthTimeout = TimeSpan.FromSeconds(90);

    private readonly JsonConfigLoader _configLoader;
    private readonly JsonManifestLoader _manifestLoader;
    private readonly IPackageVerifier _packageVerifier;
    private readonly ISystemService _systemService;
    private readonly IReleaseManager _releaseManager;
    private readonly IBackupService _backupService;
    private readonly IHealthChecker _healthChecker;
    private readonly IOtaAckClient _ackClient;

    public ApplyUpdateService(
        JsonConfigLoader configLoader,
        JsonManifestLoader manifestLoader,
        IPackageVerifier packageVerifier,
        ISystemService systemService,
        IReleaseManager releaseManager,
        IBackupService backupService,
        IHealthChecker healthChecker,
        IOtaAckClient ackClient)
    {
        _configLoader = configLoader;
        _manifestLoader = manifestLoader;
        _packageVerifier = packageVerifier;
        _systemService = systemService;
        _releaseManager = releaseManager;
        _backupService = backupService;
        _healthChecker = healthChecker;
        _ackClient = ackClient;
    }

    public async Task<int> RunAsync(
        string configPath,
        string manifestPath,
        string? packagePath,
        CancellationToken cancellationToken = default)
    {
        var config = _configLoader.Load(configPath);
        var manifest = _manifestLoader.Load(manifestPath);
        var versionPrevious = _releaseManager.GetCurrentReleaseVersion(config) ?? config.CurrentVersion;

        if (string.IsNullOrWhiteSpace(packagePath))
        {
            await SendAckAsync(config, manifest, versionPrevious, versionPrevious, "error", "download_failed", "Pacote não informado (--package).", cancellationToken);
            return 1;
        }

        var verifyResult = await _packageVerifier.VerifyAsync(manifest, packagePath, config.PublicKeyPath, cancellationToken);
        if (!verifyResult.Success)
        {
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
            await _systemService.StopServiceAsync(config.KioskServiceName, cancellationToken);
            kioskStopped = true;

            Console.WriteLine("Actualizando symlink previous ← current...");
            await _releaseManager.PointPreviousToCurrentAsync(config, cancellationToken);

            Console.WriteLine("Criando backup pré-update...");
            await _backupService.CreatePreUpdateBackupAsync(config, manifest.Version, cancellationToken);

            Console.WriteLine($"Extraindo release {manifest.Version}+{manifest.Arch}...");
            await _releaseManager.ExtractReleaseAsync(config, packagePath, manifest.Version, manifest.Arch, cancellationToken);

            Console.WriteLine("Trocando symlink current...");
            await _releaseManager.SwapCurrentToReleaseAsync(config, manifest.Version, manifest.Arch, cancellationToken);

            Console.WriteLine($"Iniciando {config.KioskServiceName}...");
            await _systemService.StartServiceAsync(config.KioskServiceName, cancellationToken);

            Console.WriteLine($"Aguardando health ({config.HealthUrl})...");
            var health = await _healthChecker.WaitForHealthyAsync(
                config.KioskServiceName,
                config.HealthUrl,
                manifest.Version,
                HealthTimeout,
                cancellationToken);

            if (!health.Success)
            {
                Console.Error.WriteLine($"Health falhou: {health.Message}");
                await RollbackAsync(config, cancellationToken);
                var rolledBackVersion = _releaseManager.GetCurrentReleaseVersion(config) ?? versionPrevious;
                await SendAckAsync(config, manifest, versionPrevious, rolledBackVersion, "rolled_back", health.ErrorCode, health.Message, cancellationToken);
                return 1;
            }

            _releaseManager.CollectGarbage(config);
            _backupService.CollectGarbage(config.BackupsDir, config.MaxReleaseFolders);

            await SendAckAsync(config, manifest, versionPrevious, manifest.Version, "success", null, null, cancellationToken);
            Console.WriteLine($"Apply concluído: {manifest.Version}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"apply falhou: {ex.Message}");

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

    private async Task RollbackAsync(Domain.ValueObjects.OtaAgentConfig config, CancellationToken cancellationToken)
    {
        Console.WriteLine("Executando rollback current → previous...");
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
}
