using System.Diagnostics;
using Jukebox.Ota.Agent.Domain.Services;
using Jukebox.Ota.Agent.Domain.ValueObjects;

namespace Jukebox.Ota.Agent.Infrastructure.Release;

public sealed class FileSystemReleaseManager : IReleaseManager
{
    public string? GetCurrentReleaseVersion(OtaAgentConfig config) =>
        ParseVersionFromReleaseFolder(ResolveSymlinkTargetName(config.CurrentSymlink));

    public string? GetPreviousReleaseVersion(OtaAgentConfig config) =>
        ParseVersionFromReleaseFolder(ResolveSymlinkTargetName(config.PreviousSymlink));

    public Task PointPreviousToCurrentAsync(OtaAgentConfig config, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var currentTarget = ResolveSymlinkTargetName(config.CurrentSymlink);
        if (string.IsNullOrEmpty(currentTarget))
        {
            throw new InvalidOperationException($"Symlink current sem alvo: {config.CurrentSymlink}");
        }

        ReplaceSymlink(config.PreviousSymlink, Path.Combine(config.ReleasesDir, currentTarget));
        return Task.CompletedTask;
    }

    public async Task ExtractReleaseAsync(
        OtaAgentConfig config,
        string packagePath,
        string version,
        string arch,
        CancellationToken cancellationToken = default)
    {
        var releaseFolderName = BuildReleaseFolderName(version, arch);
        var destDir = Path.Combine(config.ReleasesDir, releaseFolderName);
        Directory.CreateDirectory(config.ReleasesDir);

        if (Directory.Exists(destDir))
        {
            Directory.Delete(destDir, recursive: true);
        }

        Directory.CreateDirectory(destDir);

        if (Directory.Exists(packagePath))
        {
            CopyDirectory(packagePath, destDir);
            return;
        }

        if (!File.Exists(packagePath))
        {
            throw new FileNotFoundException($"Pacote não encontrado: {packagePath}");
        }

        await ExtractTarZstAsync(packagePath, destDir, cancellationToken);
    }

    public Task SwapCurrentToReleaseAsync(OtaAgentConfig config, string version, string arch, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var releaseFolderName = BuildReleaseFolderName(version, arch);
        var target = Path.Combine(config.ReleasesDir, releaseFolderName);

        if (!Directory.Exists(target))
        {
            throw new DirectoryNotFoundException($"Release não encontrada: {target}");
        }

        ReplaceSymlink(config.CurrentSymlink, target);
        return Task.CompletedTask;
    }

    public Task RollbackCurrentToPreviousAsync(OtaAgentConfig config, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var previousTargetName = ResolveSymlinkTargetName(config.PreviousSymlink);
        if (string.IsNullOrEmpty(previousTargetName))
        {
            throw new InvalidOperationException($"Symlink previous sem alvo: {config.PreviousSymlink}");
        }

        ReplaceSymlink(config.CurrentSymlink, Path.Combine(config.ReleasesDir, previousTargetName));
        return Task.CompletedTask;
    }

    public void CollectGarbage(OtaAgentConfig config)
    {
        var protectedNames = new HashSet<string>(StringComparer.Ordinal);
        AddProtectedFolder(protectedNames, config.CurrentSymlink, config.ReleasesDir);
        AddProtectedFolder(protectedNames, config.PreviousSymlink, config.ReleasesDir);
        FolderGarbageCollector.Collect(config.ReleasesDir, protectedNames, config.MaxReleaseFolders);
    }

    internal static string BuildReleaseFolderName(string version, string arch) => $"{version}+{arch}";

    private static void AddProtectedFolder(HashSet<string> protectedNames, string symlinkPath, string releasesDir)
    {
        var targetName = ResolveSymlinkTargetName(symlinkPath);
        if (!string.IsNullOrEmpty(targetName))
        {
            protectedNames.Add(targetName);
        }
    }

    private static string? ResolveSymlinkTargetName(string symlinkPath)
    {
        if (!File.Exists(symlinkPath) && !Directory.Exists(symlinkPath))
        {
            return null;
        }

        var target = Directory.ResolveLinkTarget(symlinkPath, returnFinalTarget: true)?.FullName;
        return target is null ? null : Path.GetFileName(target);
    }

    private static string? ParseVersionFromReleaseFolder(string? folderName)
    {
        if (string.IsNullOrEmpty(folderName))
        {
            return null;
        }

        var plusIndex = folderName.IndexOf('+', StringComparison.Ordinal);
        return plusIndex > 0 ? folderName[..plusIndex] : folderName;
    }

    private static void ReplaceSymlink(string symlinkPath, string targetPath)
    {
        var parent = Path.GetDirectoryName(symlinkPath);
        if (!string.IsNullOrEmpty(parent))
        {
            Directory.CreateDirectory(parent);
        }

        if (File.Exists(symlinkPath))
        {
            File.Delete(symlinkPath);
        }
        else if (Directory.Exists(symlinkPath))
        {
            Directory.Delete(symlinkPath);
        }

        Directory.CreateSymbolicLink(symlinkPath, targetPath);
    }

    private static void CopyDirectory(string sourceDir, string destDir)
    {
        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDir, file);
            var destFile = Path.Combine(destDir, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destFile)!);
            File.Copy(file, destFile, overwrite: true);
        }
    }

    private static async Task ExtractTarZstAsync(string packagePath, string destDir, CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "tar",
                Arguments = $"--zstd -xf \"{packagePath}\" -C \"{destDir}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };

        process.Start();
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
            throw new InvalidOperationException($"tar falhou (exit {process.ExitCode}): {stderr.Trim()}");
        }
    }
}
