using System.Diagnostics;
using Jukebox.Ota.Agent.Domain.ValueObjects;

namespace Jukebox.Ota.Agent.Infrastructure.Release;

/// <summary>Executa script pós-extract dentro da release (ex.: post_install.sh do streamer).</summary>
public sealed class ReleasePostInstallRunner
{
    public async Task RunAsync(
        OtaAgentConfig config,
        string releaseDirectory,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(config.PostInstallScript))
        {
            return;
        }

        var scriptPath = Path.Combine(releaseDirectory, config.PostInstallScript.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(scriptPath))
        {
            throw new FileNotFoundException($"Script pós-install não encontrado: {scriptPath}");
        }

        var psi = new ProcessStartInfo
        {
            FileName = "/bin/bash",
            Arguments = $"\"{scriptPath}\"",
            WorkingDirectory = releaseDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.Environment["INSTALL_ROOT"] = releaseDirectory;
        psi.Environment["RELEASE_DIR"] = releaseDirectory;

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Falha ao iniciar {scriptPath}");

        await process.WaitForExitAsync(cancellationToken);
        if (process.ExitCode != 0)
        {
            var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
            throw new InvalidOperationException(
                $"post_install falhou (exit {process.ExitCode}): {stderr.Trim()}");
        }
    }
}
