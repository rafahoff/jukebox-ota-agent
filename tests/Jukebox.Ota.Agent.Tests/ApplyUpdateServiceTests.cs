using Jukebox.Ota.Agent.Application.Services;
using Jukebox.Ota.Agent.Domain.Entities;
using Jukebox.Ota.Agent.Domain.Repositories;
using Jukebox.Ota.Agent.Domain.Services;
using Jukebox.Ota.Agent.Domain.ValueObjects;
using Jukebox.Ota.Agent.Infrastructure.Policy;
using Jukebox.Ota.Agent.Infrastructure.Backup;
using Jukebox.Ota.Agent.Infrastructure.Config;
using Jukebox.Ota.Agent.Infrastructure.Manifest;
using Jukebox.Ota.Agent.Infrastructure.Release;
using Jukebox.Ota.Agent.Infrastructure.Security;

namespace Jukebox.Ota.Agent.Tests;

public class ApplyUpdateServiceTests
{
    [Fact]
    public async Task RunAsync_ForaDaJanela_RecusaSemAck()
    {
        var root = CreateTempLayout();
        var configPath = WriteConfig(root);
        var manifestPath = WriteManifest(root);
        var packagePath = await CreatePackageAsync(root);

        try
        {
            var ackSpy = new AckSpy();
            var now = TimeOnly.FromDateTime(DateTime.Now);
            var policy = new OtaCheckPolicy(
                true,
                6,
                now.AddHours(2),
                now.AddHours(4));
            var service = BuildService(root, ackSpy, new FakeHealthChecker(success: true), policy);

            var exitCode = await service.RunAsync(configPath, manifestPath, packagePath);

            Assert.Equal(1, exitCode);
            Assert.Empty(ackSpy.Payloads);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_PoliticaDesabilitada_RecusaSemAck()
    {
        var root = CreateTempLayout();
        var configPath = WriteConfig(root);
        var manifestPath = WriteManifest(root);
        var packagePath = await CreatePackageAsync(root);

        try
        {
            var ackSpy = new AckSpy();
            var policy = OtaCheckPolicy.Default with { Enabled = false };
            var service = BuildService(root, ackSpy, new FakeHealthChecker(success: true), policy);

            var exitCode = await service.RunAsync(configPath, manifestPath, packagePath);

            Assert.Equal(1, exitCode);
            Assert.Empty(ackSpy.Payloads);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_SemPackage_RetornaErroEAck()
    {
        var root = CreateTempLayout();
        var configPath = WriteConfig(root);
        var manifestPath = WriteManifest(root);

        try
        {
            var ackSpy = new AckSpy();
            var service = BuildService(root, ackSpy, new FakeHealthChecker(success: true));

            var exitCode = await service.RunAsync(configPath, manifestPath, packagePath: null);

            Assert.Equal(1, exitCode);
            Assert.Single(ackSpy.Payloads);
            Assert.Equal("error", ackSpy.Payloads[0].Result);
            Assert.Equal("download_failed", ackSpy.Payloads[0].ErrorCode);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_HealthFalha_ExecutaRollbackEAckRolledBack()
    {
        var root = CreateTempLayout();
        var configPath = WriteConfig(root);
        var manifestPath = WriteManifest(root);
        var packagePath = await CreatePackageAsync(root);

        try
        {
            var ackSpy = new AckSpy();
            var service = BuildService(root, ackSpy, new FakeHealthChecker(success: false, errorCode: "health_version_mismatch"));

            var exitCode = await service.RunAsync(configPath, manifestPath, packagePath);

            Assert.Equal(1, exitCode);
            Assert.Single(ackSpy.Payloads);
            Assert.Equal("rolled_back", ackSpy.Payloads[0].Result);
            Assert.Equal("health_version_mismatch", ackSpy.Payloads[0].ErrorCode);
            Assert.True(Directory.Exists(Path.Combine(root, "backups")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_Sucesso_AckSuccessETrocaCurrent()
    {
        var root = CreateTempLayout();
        var configPath = WriteConfig(root);
        var manifestPath = WriteManifest(root);
        var packagePath = await CreatePackageAsync(root);

        try
        {
            var ackSpy = new AckSpy();
            var service = BuildService(root, ackSpy, new FakeHealthChecker(success: true));

            var exitCode = await service.RunAsync(configPath, manifestPath, packagePath);

            Assert.Equal(0, exitCode);
            Assert.Single(ackSpy.Payloads);
            Assert.Equal("success", ackSpy.Payloads[0].Result);
            Assert.Equal("1.4.2", ackSpy.Payloads[0].VersionCurrent);

            var currentTarget = Directory.ResolveLinkTarget(Path.Combine(root, "current"), returnFinalTarget: true);
            Assert.NotNull(currentTarget);
            Assert.EndsWith("1.4.2+aarch64", currentTarget.FullName, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static ApplyUpdateService BuildService(
        string root,
        AckSpy ackSpy,
        IHealthChecker healthChecker,
        OtaCheckPolicy? policy = null)
    {
        var effectivePolicy = policy ?? OtaCheckPolicy.Default;
        var config = new OtaAgentConfig(
            "machine-test",
            "beta",
            "file:///tmp/manifest.json",
            "1.4.1",
            string.Empty,
            "fake_kiosk",
            Path.Combine(root, "releases"),
            Path.Combine(root, "current"),
            Path.Combine(root, "previous"),
            Path.Combine(root, "backups"),
            "http://127.0.0.1:9/health",
            Path.Combine(root, "kiosk-data"),
            7);

        var releaseManager = new FileSystemReleaseManager();
        SeedCurrentRelease(root, config);

        return new ApplyUpdateService(
            new JsonConfigLoader(),
            new JsonManifestLoader(),
            new FakePackageVerifier(),
            new FakeSystemService(),
            releaseManager,
            new FileSystemBackupService(),
            healthChecker,
            ackSpy,
            new FakePolicyProvider(effectivePolicy));
    }

    private sealed class FakePolicyProvider(OtaCheckPolicy policy) : Domain.Services.IOtaPolicyProvider
    {
        public OtaCheckPolicy GetPolicy(OtaAgentConfig config) => policy;
    }

    private static void SeedCurrentRelease(string root, OtaAgentConfig config)
    {
        var releaseDir = Path.Combine(config.ReleasesDir, "1.4.1+aarch64");
        Directory.CreateDirectory(releaseDir);
        File.WriteAllText(Path.Combine(releaseDir, "app.txt"), "old");

        Directory.CreateSymbolicLink(config.CurrentSymlink, releaseDir);
        Directory.CreateSymbolicLink(config.PreviousSymlink, releaseDir);

        var kioskData = Path.Combine(root, "kiosk-data");
        Directory.CreateDirectory(kioskData);
        File.WriteAllText(Path.Combine(kioskData, "jukebox_library.db"), "db");
        File.WriteAllText(Path.Combine(kioskData, "shared_preferences.json"), "{}");
    }

    private static string CreateTempLayout()
    {
        var root = Path.Combine(Path.GetTempPath(), $"ota-apply-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(Path.Combine(root, "releases"));
        Directory.CreateDirectory(Path.Combine(root, "backups"));
        return root;
    }

    private static string WriteConfig(string root)
    {
        var path = Path.Combine(root, "ota-agent.json");
        File.WriteAllText(path, $$"""
            {
              "device_id": "machine-test",
              "channel": "beta",
              "ota_base_url": "file:///tmp/manifest.json",
              "current_version": "1.4.1",
              "public_key_path": "",
              "kiosk_service_name": "fake_kiosk",
              "releases_dir": "{{Path.Combine(root, "releases").Replace("\\", "\\\\")}}",
              "current_symlink": "{{Path.Combine(root, "current").Replace("\\", "\\\\")}}",
              "previous_symlink": "{{Path.Combine(root, "previous").Replace("\\", "\\\\")}}",
              "backups_dir": "{{Path.Combine(root, "backups").Replace("\\", "\\\\")}}",
              "health_url": "http://127.0.0.1:9/health",
              "kiosk_data_dir": "{{Path.Combine(root, "kiosk-data").Replace("\\", "\\\\")}}",
              "max_release_folders": 7
            }
            """);
        return path;
    }

    private static string WriteManifest(string root)
    {
        var path = Path.Combine(root, "manifest.json");
        File.WriteAllText(path, """
            {
              "app": "jukeeo",
              "version": "1.4.2",
              "arch": "aarch64",
              "package_type": "full",
              "sha256": "PLACEHOLDER",
              "signature_b64": "",
              "signature_algorithm": "rsa-pss-sha256",
              "released_at": "2026-06-12T12:00:00Z"
            }
            """);
        return path;
    }

    private static async Task<string> CreatePackageAsync(string root)
    {
        var packageDir = Path.Combine(root, "package-src");
        Directory.CreateDirectory(packageDir);
        await File.WriteAllTextAsync(Path.Combine(packageDir, "app.txt"), "new");
        return packageDir;
    }

    private sealed class FakePackageVerifier : IPackageVerifier
    {
        public Task<PackageVerificationResult> VerifyAsync(
            UpdateManifest manifest,
            string packagePath,
            string publicKeyPath,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PackageVerificationResult(true, "verificação simulada"));
    }

    private sealed class FakeSystemService : ISystemService
    {
        public Task StopServiceAsync(string serviceName, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task StartServiceAsync(string serviceName, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<bool> IsServiceActiveAsync(string serviceName, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }

    private sealed class FakeHealthChecker : IHealthChecker
    {
        private readonly bool _success;
        private readonly string? _errorCode;

        public FakeHealthChecker(bool success, string? errorCode = null)
        {
            _success = success;
            _errorCode = errorCode;
        }

        public Task<HealthCheckResult> WaitForHealthyAsync(
            string serviceName,
            string healthUrl,
            string expectedAppVersion,
            TimeSpan timeout,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_success
                ? new HealthCheckResult(true, null, null)
                : new HealthCheckResult(false, _errorCode ?? "health_check_timeout", "falha simulada"));
    }

    private sealed class AckSpy : IOtaAckClient
    {
        public List<UpdateAckPayload> Payloads { get; } = [];

        public Task SendAckAsync(OtaAgentConfig config, UpdateAckPayload payload, CancellationToken cancellationToken = default)
        {
            Payloads.Add(payload);
            return Task.CompletedTask;
        }
    }
}
