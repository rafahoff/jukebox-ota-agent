using Jukebox.Ota.Agent.Application.Services;
using Jukebox.Ota.Agent.Domain.ValueObjects;
using Jukebox.Ota.Agent.Infrastructure.Config;
using Jukebox.Ota.Agent.Infrastructure.ExternalServices;
using Jukebox.Ota.Agent.Infrastructure.Manifest;
using Jukebox.Ota.Agent.Infrastructure.Policy;
using Jukebox.Ota.Agent.Infrastructure.Release;
using Jukebox.Ota.Agent.Infrastructure.Security;
using Jukebox.Ota.Agent.Infrastructure.Telemetry;

namespace Jukebox.Ota.Agent.Tests;

public class CheckUpdateServiceTests
{
    [Fact]
    public async Task RunAsync_ComFixtureLocal_BaixaPacoteERetornaReadyToApply()
    {
        var root = Path.Combine(Path.GetTempPath(), $"ota-check-{Guid.NewGuid():N}");

        try
        {
            var fixture = await OtaTestFixture.CreateAsync(root);
            using var client = new HttpOtaUpdateClient();
            var service = BuildService(client);

            var exitCode = await service.RunAsync(fixture.ConfigPath);

            Assert.Equal(2, exitCode);

            var config = new JsonConfigLoader().Load(fixture.ConfigPath);
            var statusStore = new FileOtaUpdateStatusStore();
            var status = statusStore.Read(config);
            Assert.True(status.UpdateAvailable);
            Assert.Equal(fixture.RemoteVersion, status.RemoteVersion);
            Assert.Equal(OtaUpdatePhases.ReadyToApply, status.Phase);

            var downloadDir = OtaDownloadCache.GetDownloadDirectory(config);
            Assert.True(Directory.Exists(downloadDir));
            Assert.True(File.Exists(OtaDownloadCache.GetManifestPath(config, fixture.RemoteVersion)));
            Assert.True(File.Exists(OtaDownloadCache.GetPackagePath(config, new Domain.Entities.UpdateManifest(
                "jukeeo", fixture.RemoteVersion, "aarch64", "", "", "rsa-pss-sha256", DateTimeOffset.UtcNow))));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static CheckUpdateService BuildService(HttpOtaUpdateClient client) =>
        new(
            new JsonConfigLoader(),
            new OtaConfigVersionSync(new JsonConfigWriter(), new FileSystemReleaseManager()),
            client,
            new ConsoleTelemetryReporter(),
            new AlwaysAllowPolicyProvider(),
            new FileOtaUpdateStatusStore(),
            new RsaPssPackageVerifier(),
            new JsonManifestWriter());

    private sealed class AlwaysAllowPolicyProvider : Domain.Services.IOtaPolicyProvider
    {
        public OtaCheckPolicy GetPolicy(Domain.ValueObjects.OtaAgentConfig config) =>
            OtaCheckPolicy.Default with { IntervalMinutes = 15 };
    }
}
