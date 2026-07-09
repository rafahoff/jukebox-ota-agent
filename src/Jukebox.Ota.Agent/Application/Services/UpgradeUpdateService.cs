using Jukebox.Ota.Agent.Domain.Services;
using Jukebox.Ota.Agent.Domain.ValueObjects;
using Jukebox.Ota.Agent.Infrastructure.Config;
using Jukebox.Ota.Agent.Infrastructure.Logging;
using Jukebox.Ota.Agent.Infrastructure.Manifest;
using Jukebox.Ota.Agent.Infrastructure.Policy;

namespace Jukebox.Ota.Agent.Application.Services;

/// <summary>Aplica pacote OTA já baixado pelo <see cref="CheckUpdateService"/> (apply-only).</summary>
public sealed class UpgradeUpdateService
{
    private readonly JsonConfigLoader _configLoader;
    private readonly ApplyUpdateService _applyService;
    private readonly IOtaUpdateStatusStore _statusStore;
    private readonly JsonManifestLoader _manifestLoader;

    public UpgradeUpdateService(
        JsonConfigLoader configLoader,
        ApplyUpdateService applyService,
        IOtaUpdateStatusStore statusStore,
        JsonManifestLoader manifestLoader)
    {
        _configLoader = configLoader;
        _applyService = applyService;
        _statusStore = statusStore;
        _manifestLoader = manifestLoader;
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
            var status = _statusStore.Read(config);

            if (string.IsNullOrWhiteSpace(status.RemoteVersion))
            {
                Console.WriteLine("Nenhuma atualização pronta para aplicar (remote_version ausente).");
                return 0;
            }

            var remoteVersion = status.RemoteVersion;
            string manifestPath;
            string packagePath;

            if (!OtaDownloadCache.TryResolveReadyCache(
                    config,
                    remoteVersion,
                    ResolveArchFromCache(config, remoteVersion),
                    out manifestPath,
                    out packagePath))
            {
                if (OtaDownloadCache.TryFindAnyCachedManifest(config, out var staleManifestPath))
                {
                    var staleManifest = _manifestLoader.Load(staleManifestPath);
                    if (!string.Equals(staleManifest.Version, remoteVersion, StringComparison.Ordinal))
                    {
                        return RejectVersionMismatch(config, status, staleManifest.Version, remoteVersion);
                    }

                    // Manifesto existe e coincide; tentar resolver pacote pela arch do manifesto.
                    manifestPath = staleManifestPath;
                    packagePath = OtaDownloadCache.GetPackagePath(config, staleManifest);
                    if (!File.Exists(packagePath))
                    {
                        Console.WriteLine(
                            $"Nenhuma atualização pronta para aplicar (pacote ausente para {remoteVersion}).");
                        return 0;
                    }
                }
                else
                {
                    Console.WriteLine(
                        $"Nenhuma atualização pronta para aplicar (cache ausente para {remoteVersion}).");
                    return 0;
                }
            }

            var manifest = _manifestLoader.Load(manifestPath);

            // A2: recusa apply se versão em cache ≠ remote_version no status
            if (!string.Equals(manifest.Version, remoteVersion, StringComparison.Ordinal))
            {
                return RejectVersionMismatch(config, status, manifest.Version, remoteVersion);
            }

            if (status.Phase != OtaUpdatePhases.ReadyToApply)
            {
                Console.WriteLine(
                    $"Aplicando pacote em cache (phase={status.Phase}; esperado {OtaUpdatePhases.ReadyToApply}).");
            }

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

    private int RejectVersionMismatch(
        OtaAgentConfig config,
        OtaUpdateStatus status,
        string cacheVersion,
        string remoteVersion)
    {
        var message =
            $"Apply recusado: versão em cache ({cacheVersion}) difere de remote_version ({remoteVersion}).";
        Console.Error.WriteLine(message);
        FileAgentLogger.LogApply(message);
        WriteStatus(config, s => s with
        {
            Phase = OtaUpdatePhases.Error,
            CurrentVersion = config.CurrentVersion,
            ErrorMessage = message,
        });
        return 1;
    }

    private static string ResolveArchFromCache(OtaAgentConfig config, string version)
    {
        var downloadDir = OtaDownloadCache.GetDownloadDirectory(config);
        if (!Directory.Exists(downloadDir))
        {
            return "aarch64";
        }

        var prefix = $"jukeeo-{version}+";
        foreach (var file in Directory.EnumerateFiles(downloadDir, $"{prefix}*.tar.zst"))
        {
            var fileName = Path.GetFileName(file);
            if (!fileName.EndsWith(".tar.zst", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Path.GetFileNameWithoutExtension só remove .zst — tratar .tar.zst explicitamente.
            var nameWithoutExtension = fileName[..^".tar.zst".Length];
            var plusIndex = nameWithoutExtension.IndexOf('+', StringComparison.Ordinal);
            if (plusIndex >= 0 && plusIndex < nameWithoutExtension.Length - 1)
            {
                return nameWithoutExtension[(plusIndex + 1)..];
            }
        }

        return "aarch64";
    }

    private void WriteStatus(OtaAgentConfig config, Func<OtaUpdateStatus, OtaUpdateStatus> mutator)
    {
        var current = _statusStore.Read(config);
        _statusStore.Write(config, mutator(current));
    }
}
