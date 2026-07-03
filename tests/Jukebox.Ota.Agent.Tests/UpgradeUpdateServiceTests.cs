using Jukebox.Ota.Agent.Application.Services;
using Jukebox.Ota.Agent.Domain.Repositories;
using Jukebox.Ota.Agent.Domain.Services;
using Jukebox.Ota.Agent.Domain.ValueObjects;
using Jukebox.Ota.Agent.Infrastructure.Backup;
using Jukebox.Ota.Agent.Infrastructure.Config;
using Jukebox.Ota.Agent.Infrastructure.Manifest;
using Jukebox.Ota.Agent.Infrastructure.Policy;
using Jukebox.Ota.Agent.Infrastructure.Release;
using Jukebox.Ota.Agent.Infrastructure.Security;

namespace Jukebox.Ota.Agent.Tests;

public class UpgradeUpdateServiceTests
{
    [Fact]
    public async Task RunAsync_SemCache_RetornaZero()
    {
        var root = Path.Combine(Path.GetTempPath(), $"ota-upgrade-{Guid.NewGuid():N}");

        try
        {
            var fixture = await OtaTestFixture.CreateAsync(root);
            var config = new JsonConfigLoader().Load(fixture.ConfigPath);
            var statusStore = new FileOtaUpdateStatusStore();
            statusStore.Write(config, new OtaUpdateStatus(
                Phase: OtaUpdatePhases.ReadyToApply,
                RemoteVersion: fixture.RemoteVersion,
                UpdateAvailable: true,
                CurrentVersion: "1.4.1"));

            var service = BuildUpgradeService(statusStore, OtaCheckPolicy.Default);

            var exitCode = await service.RunAsync(fixture.ConfigPath);

            Assert.Equal(0, exitCode);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_VersaoCacheDiferenteDeRemoteVersion_RecusaA2()
    {
        var root = Path.Combine(Path.GetTempPath(), $"ota-upgrade-{Guid.NewGuid():N}");

        try
        {
            var fixture = await OtaTestFixture.CreateAsync(root, remoteVersion: "1.4.2");
            var config = new JsonConfigLoader().Load(fixture.ConfigPath);
            await SeedCacheAsync(fixture, config);

            var statusStore = new FileOtaUpdateStatusStore();
            statusStore.Write(config, new OtaUpdateStatus(
                Phase: OtaUpdatePhases.ReadyToApply,
                RemoteVersion: "9.9.9",
                UpdateAvailable: true,
                CurrentVersion: "1.4.1"));

            var service = BuildUpgradeService(statusStore, OtaCheckPolicy.Default);
            var exitCode = await service.RunAsync(fixture.ConfigPath);

            Assert.Equal(1, exitCode);
            var status = statusStore.Read(config);
            Assert.Equal(OtaUpdatePhases.Error, status.Phase);
            Assert.Contains("9.9.9", status.ErrorMessage);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_ComCachePronto_DelegaApplySemNovoDownload()
    {
        var root = Path.Combine(Path.GetTempPath(), $"ota-upgrade-{Guid.NewGuid():N}");

        try
        {
            var fixture = await OtaTestFixture.CreateAsync(root);
            var config = new JsonConfigLoader().Load(fixture.ConfigPath);
            await SeedCacheAsync(fixture, config);

            var statusStore = new FileOtaUpdateStatusStore();
            statusStore.Write(config, new OtaUpdateStatus(
                Phase: OtaUpdatePhases.ReadyToApply,
                RemoteVersion: fixture.RemoteVersion,
                UpdateAvailable: true,
                CurrentVersion: "1.4.1"));

            // Política desabilitada: apply é invocado (phase muda) e recusa.
            var service = BuildUpgradeService(statusStore, OtaCheckPolicy.Default with { Enabled = false });
            var exitCode = await service.RunAsync(fixture.ConfigPath);

            Assert.Equal(1, exitCode);
            var status = statusStore.Read(config);
            Assert.Equal(OtaUpdatePhases.Error, status.Phase);
            Assert.Contains("desabilitada", status.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void FileOtaUpdateStatusStore_GravaFaseReadyToApply()
    {
        var root = Path.Combine(Path.GetTempPath(), $"ota-phase-{Guid.NewGuid():N}");
        var kioskData = Path.Combine(root, "kiosk-data");
        Directory.CreateDirectory(kioskData);
        var config = new OtaAgentConfig(
            "machine-test",
            "beta",
            "file:///tmp/manifest.json",
            "1.0.0",
            string.Empty,
            KioskDataDir: kioskData);

        try
        {
            var store = new FileOtaUpdateStatusStore();
            store.Write(config, new OtaUpdateStatus(
                Phase: OtaUpdatePhases.ReadyToApply,
                CheckedAtMs: 1_700_000_000_000,
                CurrentVersion: "1.0.0",
                RemoteVersion: "1.1.0",
                UpdateAvailable: true));

            var json = File.ReadAllText(Path.Combine(kioskData, "ota_update_status.json"));
            Assert.Contains("\"phase\": \"ready_to_apply\"", json);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static UpgradeUpdateService BuildUpgradeService(
        IOtaUpdateStatusStore statusStore,
        OtaCheckPolicy policy)
    {
        var configLoader = new JsonConfigLoader();
        var applyService = new ApplyUpdateService(
            configLoader,
            new OtaConfigVersionSync(new JsonConfigWriter(), new FileSystemReleaseManager()),
            new JsonManifestLoader(),
            new RsaPssPackageVerifier(),
            new FakeSystemService(),
            new FileSystemReleaseManager(),
            new FileSystemBackupService(),
            new FakeHealthChecker(),
            new AckSpy(),
            new FakePolicyProvider(policy),
            statusStore);

        return new UpgradeUpdateService(configLoader, applyService, statusStore, new JsonManifestLoader());
    }

    private static async Task SeedCacheAsync(OtaTestFixture.OtaFixturePaths fixture, OtaAgentConfig config)
    {
        var downloadDir = OtaDownloadCache.GetDownloadDirectory(config);
        Directory.CreateDirectory(downloadDir);

        var manifest = new JsonManifestLoader().Load(fixture.ManifestPath);
        new JsonManifestWriter().Write(OtaDownloadCache.GetManifestPath(config, manifest.Version), manifest);

        var packageDest = OtaDownloadCache.GetPackagePath(config, manifest);
        await File.WriteAllTextAsync(packageDest, await File.ReadAllTextAsync(fixture.PackageSourcePath));
    }

    private sealed class FakeSystemService : ISystemService
    {
        public Task StopServiceAsync(string serviceName, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task StartServiceAsync(string serviceName, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<bool> IsServiceActiveAsync(string serviceName, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<bool> IsServiceUnitInstalledAsync(string serviceName, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }

    private sealed class FakeHealthChecker : IHealthChecker
    {
        public Task<HealthCheckResult> WaitForHealthyAsync(
            string serviceName,
            string healthUrl,
            string expectedVersion,
            TimeSpan timeout,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new HealthCheckResult(true, null, null));
    }

    private sealed class AckSpy : IOtaAckClient
    {
        public Task SendAckAsync(
            OtaAgentConfig config,
            Domain.Entities.UpdateAckPayload payload,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FakePolicyProvider(OtaCheckPolicy policy) : IOtaPolicyProvider
    {
        public OtaCheckPolicy GetPolicy(OtaAgentConfig config) => policy;
    }
}
