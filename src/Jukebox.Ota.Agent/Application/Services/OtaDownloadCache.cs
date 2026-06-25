using Jukebox.Ota.Agent.Domain.Entities;
using Jukebox.Ota.Agent.Domain.ValueObjects;

namespace Jukebox.Ota.Agent.Application.Services;

/// <summary>Paths do cache OTA em <c>{state_directory}/downloads/</c>.</summary>
public static class OtaDownloadCache
{
    public static string GetDownloadDirectory(OtaAgentConfig config) =>
        Path.Combine(config.StateDirectory, "downloads");

    public static string GetManifestPath(OtaAgentConfig config, string version) =>
        Path.Combine(GetDownloadDirectory(config), $"jukeeo-{version}-manifest.json");

    public static string GetPackagePath(OtaAgentConfig config, UpdateManifest manifest) =>
        Path.Combine(GetDownloadDirectory(config), $"jukeeo-{manifest.Version}+{manifest.Arch}.tar.zst");

    /// <summary>Verifica se manifesto e pacote existem para a versão indicada.</summary>
    public static bool TryResolveReadyCache(
        OtaAgentConfig config,
        string version,
        string arch,
        out string manifestPath,
        out string packagePath)
    {
        manifestPath = GetManifestPath(config, version);
        packagePath = Path.Combine(GetDownloadDirectory(config), $"jukeeo-{version}+{arch}.tar.zst");
        return File.Exists(manifestPath) && File.Exists(packagePath);
    }

    /// <summary>Procura qualquer manifesto em cache (para validação A2).</summary>
    public static bool TryFindAnyCachedManifest(OtaAgentConfig config, out string manifestPath)
    {
        manifestPath = string.Empty;
        var downloadDir = GetDownloadDirectory(config);
        if (!Directory.Exists(downloadDir))
        {
            return false;
        }

        foreach (var file in Directory.EnumerateFiles(downloadDir, "jukeeo-*-manifest.json"))
        {
            manifestPath = file;
            return true;
        }

        return false;
    }
}
