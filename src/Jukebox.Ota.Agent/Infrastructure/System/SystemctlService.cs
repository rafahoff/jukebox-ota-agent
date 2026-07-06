using System.Diagnostics;
using System.Runtime.InteropServices;
using Jukebox.Ota.Agent.Domain.Services;

namespace Jukebox.Ota.Agent.Infrastructure.System;

/// <summary>Executa systemctl via Process (Pi/Linux); usa sudo -n quando não é root.</summary>
public sealed class SystemctlService : ISystemService
{
    public async Task StopServiceAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        var exitCode = await RunSystemctlAsync($"stop {serviceName}", cancellationToken, throwOnError: false);
        // Greenfield: unit ainda não instalada (autostart pendente).
        if (exitCode is 0 or 5)
        {
            return;
        }

        throw new InvalidOperationException(
            $"systemctl stop {serviceName} falhou (exit {exitCode})");
    }

    /// <summary>
    /// Detecta se a unit existe via <c>systemctl is-active</c> (permitido no sudoers do Pi).
    /// Exit 0 (active) ou 3 (inactive/failed) ⇒ instalada; 4 ⇒ not-found.
    /// </summary>
    public async Task<bool> IsServiceUnitInstalledAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        var exitCode = await RunSystemctlAsync($"is-active {serviceName}", cancellationToken, throwOnError: false);
        return IsServiceUnitInstalledFromIsActiveExitCode(exitCode);
    }

    /// <summary>Interpreta exit code de <c>systemctl is-active</c> para presença da unit.</summary>
    public static bool IsServiceUnitInstalledFromIsActiveExitCode(int exitCode) =>
        exitCode is 0 or 3;

    public async Task StartServiceAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        await RunSystemctlAsync($"start {serviceName}", cancellationToken);
    }

    public async Task<bool> IsServiceActiveAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        var exitCode = await RunSystemctlAsync($"is-active {serviceName}", cancellationToken, throwOnError: false);
        return exitCode == 0;
    }

    private static async Task<int> RunSystemctlAsync(
        string arguments,
        CancellationToken cancellationToken,
        bool throwOnError = true)
    {
        var (fileName, processArgs) = BuildSystemctlInvocation(NormalizeSystemctlArguments(arguments));

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = processArgs,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };

        process.Start();
        await process.WaitForExitAsync(cancellationToken);

        if (throwOnError && process.ExitCode != 0)
        {
            var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
            throw new InvalidOperationException(
                $"{fileName} {processArgs} falhou (exit {process.ExitCode}): {stderr.Trim()}");
        }

        return process.ExitCode;
    }

    public static (string FileName, string Arguments) BuildSystemctlInvocation(string systemctlArguments)
    {
        if (ShouldUseSudoForSystemctl())
        {
            return ("/usr/bin/sudo", $"-n /bin/systemctl {systemctlArguments}");
        }

        return ("/bin/systemctl", systemctlArguments);
    }

    /// <summary>Garante sufixo <c>.service</c> — o sudoers exige o nome completo da unit.</summary>
    public static string NormalizeSystemctlArguments(string systemctlArguments)
    {
        var parts = systemctlArguments.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            return systemctlArguments;
        }

        var verb = parts[0];
        if (verb is not ("start" or "stop" or "restart" or "is-active" or "cat"))
        {
            return systemctlArguments;
        }

        var unit = parts[1];
        if (!unit.EndsWith(".service", StringComparison.Ordinal))
        {
            unit += ".service";
        }

        return $"{verb} {unit}";
    }

    private static bool ShouldUseSudoForSystemctl()
    {
        if (!OperatingSystem.IsLinux())
        {
            return false;
        }

        return GetEffectiveUserId() != 0;
    }

    private static uint GetEffectiveUserId()
    {
        if (!OperatingSystem.IsLinux())
        {
            return 0;
        }

        return geteuid();
    }

    [DllImport("libc", EntryPoint = "geteuid")]
    private static extern uint geteuid();
}
