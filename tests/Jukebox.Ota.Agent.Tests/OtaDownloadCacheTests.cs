using Jukebox.Ota.Agent.Application.Services;
using Jukebox.Ota.Agent.Domain.ValueObjects;

namespace Jukebox.Ota.Agent.Tests;

public class OtaDownloadCacheTests
{
    [Fact]
    public void GetDownloadDirectory_UsaSubpastaDownloadsNoStateDirectory()
    {
        var config = new OtaAgentConfig(
            DeviceId: "dev",
            Channel: "beta",
            OtaBaseUrl: "http://localhost",
            CurrentVersion: "1.0.0",
            PublicKeyPath: "/etc/key.pem",
            KioskServiceName: "kiosk.service",
            ReleasesDir: "/opt/releases",
            CurrentSymlink: "/opt/current",
            PreviousSymlink: "/opt/previous",
            BackupsDir: "/opt/backups",
            HealthUrl: "http://127.0.0.1/health",
            KioskDataDir: "/tmp/kiosk",
            StateDirectory: "/var/lib/jukebox-ota");

        var path = OtaDownloadCache.GetDownloadDirectory(config);

        Assert.Equal(Path.Combine("/var/lib/jukebox-ota", "downloads"), path);
        Assert.EndsWith("downloads", path);
        Assert.DoesNotContain($"{Path.DirectorySeparatorChar}download{Path.DirectorySeparatorChar}", path);
    }

    [Fact]
    public void GetPackagePath_SegueConvencaoDeNomeDoPacote()
    {
        var config = new OtaAgentConfig(
            DeviceId: "dev",
            Channel: "beta",
            OtaBaseUrl: "http://localhost",
            CurrentVersion: "1.0.0",
            PublicKeyPath: "/etc/key.pem",
            KioskServiceName: "kiosk.service",
            ReleasesDir: "/opt/releases",
            CurrentSymlink: "/opt/current",
            PreviousSymlink: "/opt/previous",
            BackupsDir: "/opt/backups",
            HealthUrl: "http://127.0.0.1/health",
            KioskDataDir: "/tmp/kiosk",
            StateDirectory: "/var/lib/jukebox-ota");

        var manifest = new Domain.Entities.UpdateManifest(
            "jukeeo", "1.0.17", "aarch64", "", "", "rsa-pss-sha256", DateTimeOffset.UtcNow);

        var packagePath = OtaDownloadCache.GetPackagePath(config, manifest);

        Assert.Equal(
            Path.Combine("/var/lib/jukebox-ota", "downloads", "jukeeo-1.0.17+aarch64.tar.zst"),
            packagePath);
    }
}
