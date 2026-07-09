using Jukebox.Ota.Agent.Domain.Services;
using Jukebox.Ota.Agent.Domain.ValueObjects;
using Jukebox.Ota.Agent.Infrastructure.Config;
using Jukebox.Ota.Agent.Infrastructure.Logging;

namespace Jukebox.Ota.Agent.Application.Services;

/// <summary>
/// Mantém <c>current_version</c> do config alinhada com o symlink <c>/opt/jukeeo/current</c>
/// e com a versão aplicada com sucesso.
/// </summary>
public sealed class OtaConfigVersionSync
{
    private readonly JsonConfigWriter _configWriter;
    private readonly IReleaseManager _releaseManager;

    public OtaConfigVersionSync(JsonConfigWriter configWriter, IReleaseManager releaseManager)
    {
        _configWriter = configWriter;
        _releaseManager = releaseManager;
    }

    /// <summary>
    /// Se a release instalada diverge do config, atualiza o JSON e devolve config em memória coerente.
    /// </summary>
    public OtaAgentConfig ResolveAndSync(string configPath, OtaAgentConfig config)
    {
        var fromRelease = _releaseManager.GetCurrentReleaseVersion(config);
        if (string.IsNullOrWhiteSpace(fromRelease)
            || string.Equals(fromRelease, config.CurrentVersion, StringComparison.Ordinal))
        {
            return config;
        }

        try
        {
            _configWriter.UpdateCurrentVersion(configPath, fromRelease);
            Console.WriteLine($"current_version sincronizada no config: {config.CurrentVersion} → {fromRelease}");
            FileAgentLogger.LogCheck($"current_version sincronizada no config: {config.CurrentVersion} → {fromRelease}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"AVISO: não foi possível gravar current_version no config ({ex.Message}); usando {fromRelease} nesta execução.");
        }

        return config with { CurrentVersion = fromRelease };
    }

    public void PersistCurrentVersion(string configPath, string version)
    {
        _configWriter.UpdateCurrentVersion(configPath, version);
        Console.WriteLine($"current_version atualizada no config: {version}");
        FileAgentLogger.LogApply($"current_version atualizada no config: {version}");
    }
}
