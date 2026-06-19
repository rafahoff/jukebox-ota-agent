using Jukebox.Ota.Agent.Domain.Entities;
using Jukebox.Ota.Agent.Domain.Repositories;
using Jukebox.Ota.Agent.Domain.Services;
using Jukebox.Ota.Agent.Domain.ValueObjects;
using Jukebox.Ota.Agent.Infrastructure.Config;
using Jukebox.Ota.Agent.Infrastructure.Logging;
using Jukebox.Ota.Agent.Infrastructure.Manifest;
using Jukebox.Ota.Agent.Infrastructure.Policy;

namespace Jukebox.Ota.Agent.Application.Services;

/// <summary>Orquestra check → download → apply num único fluxo operador.</summary>
public sealed class UpgradeUpdateService
{
    private readonly JsonConfigLoader _configLoader;
    private readonly CheckUpdateService _checkService;
    private readonly ApplyUpdateService _applyService;
    private readonly IOtaUpdateClient _otaClient;
    private readonly IOtaUpdateStatusStore _statusStore;
    private readonly JsonManifestWriter _manifestWriter;

    public UpgradeUpdateService(
        JsonConfigLoader configLoader,
        CheckUpdateService checkService,
        ApplyUpdateService applyService,
        IOtaUpdateClient otaClient,
        IOtaUpdateStatusStore statusStore,
        JsonManifestWriter manifestWriter)
    {
        _configLoader = configLoader;
        _checkService = checkService;
        _applyService = applyService;
        _otaClient = otaClient;
        _statusStore = statusStore;
        _manifestWriter = manifestWriter;
    }

    public async Task<int> RunAsync(
        string configPath,
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        OtaAgentConfig? config = null;

        try
        {
            config = _configLoader.Load(configPath);
            var checkOutcome = await _checkService.ExecuteAsync(configPath, force, cancellationToken);

            if (checkOutcome.ExitCode == 1)
            {
                return 1;
            }

            if (!checkOutcome.UpdateAvailable || checkOutcome.Manifest is null)
            {
                Console.WriteLine("Nenhuma atualização disponível para aplicar.");
                return 0;
            }

            var manifest = checkOutcome.Manifest;
            var downloadDir = Path.Combine(config.StateDirectory, "downloads");
            Directory.CreateDirectory(downloadDir);

            WriteStatus(config, status => status with
            {
                Phase = OtaUpdatePhases.Downloading,
                CurrentVersion = config.CurrentVersion,
                RemoteVersion = manifest.Version,
                UpdateAvailable = true,
                ErrorMessage = null,
            });

            Console.WriteLine($"Descarregando pacote {manifest.Version}+{manifest.Arch}...");
            FileAgentLogger.LogApply($"Descarregando pacote {manifest.Version}+{manifest.Arch}...");
            var packagePath = await _otaClient.DownloadPackageAsync(config, manifest, downloadDir, cancellationToken);

            var manifestPath = Path.Combine(downloadDir, $"jukeeo-{manifest.Version}-manifest.json");
            _manifestWriter.Write(manifestPath, manifest);

            return await _applyService.RunAsync(configPath, manifestPath, packagePath, force, cancellationToken);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"upgrade falhou: {ex.Message}");
            FileAgentLogger.LogApply($"upgrade falhou: {ex.Message}");

            if (config is not null)
            {
                WriteStatus(config, status => status with
                {
                    Phase = OtaUpdatePhases.Error,
                    CurrentVersion = config.CurrentVersion,
                    ErrorMessage = ex.Message,
                });
            }

            return 1;
        }
    }

    private void WriteStatus(OtaAgentConfig config, Func<OtaUpdateStatus, OtaUpdateStatus> mutator)
    {
        var current = _statusStore.Read(config);
        _statusStore.Write(config, mutator(current));
    }
}
