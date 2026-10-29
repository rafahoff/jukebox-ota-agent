using Jukebox.Ota.Agent.Domain.ValueObjects;
using Jukebox.Ota.Agent.Infrastructure.Release;

namespace Jukebox.Ota.Agent.Tests;

public class FileSystemReleaseManagerCliSymlinkTests
{
    [Fact]
    public async Task SyncInstallRootSymlinkAsync_AtualizaPathCliQuandoConfigurado()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var root = Path.Combine(Path.GetTempPath(), $"ota-cli-symlink-{Guid.NewGuid():N}");
        var releases = Path.Combine(root, "releases");
        var releaseDir = Path.Combine(releases, "0.6.58+aarch64");
        var currentLink = Path.Combine(root, "current");
        var installRoot = Path.Combine(root, "streamer");
        var cliLink = Path.Combine(root, "bin", "jukeeo-streamer");

        Directory.CreateDirectory(releaseDir);
        await File.WriteAllTextAsync(Path.Combine(releaseDir, "jukeeo-streamer"), "#!/bin/sh\n");

        var config = new OtaAgentConfig(
            DeviceId: "test",
            Channel: "streamer-beta",
            OtaBaseUrl: "https://ota.example",
            CurrentVersion: "0.6.57",
            PublicKeyPath: "/etc/jukeeo/ota-public-key.pem",
            App: "jukeeo-streamer",
            ReleasesDir: releases,
            CurrentSymlink: currentLink,
            PreviousSymlink: Path.Combine(root, "previous"),
            InstallRootSymlink: installRoot,
            CliSymlinkPath: cliLink,
            CliSymlinkBinary: "jukeeo-streamer");

        Directory.CreateSymbolicLink(currentLink, releaseDir);

        var manager = new FileSystemReleaseManager();
        await manager.SyncInstallRootSymlinkAsync(config);

        Assert.True(Directory.Exists(installRoot));
        Assert.Equal(releaseDir, Directory.ResolveLinkTarget(installRoot, returnFinalTarget: true)?.FullName);
        Assert.True(File.Exists(cliLink));
        Assert.Equal(
            Path.Combine(releaseDir, "jukeeo-streamer"),
            File.ResolveLinkTarget(cliLink, returnFinalTarget: true)?.FullName);
    }
}
