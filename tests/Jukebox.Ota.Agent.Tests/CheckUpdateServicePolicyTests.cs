using Jukebox.Ota.Agent.Application.Services;
using Jukebox.Ota.Agent.Domain.Repositories;
using Jukebox.Ota.Agent.Domain.Services;
using Jukebox.Ota.Agent.Domain.ValueObjects;
using Jukebox.Ota.Agent.Infrastructure.Config;
using Jukebox.Ota.Agent.Infrastructure.ExternalServices;
using Jukebox.Ota.Agent.Infrastructure.Policy;
using Jukebox.Ota.Agent.Infrastructure.Release;
using Jukebox.Ota.Agent.Infrastructure.Telemetry;

namespace Jukebox.Ota.Agent.Tests;

public class CheckUpdateServicePolicyTests
{
    [Fact]
    public async Task RunAsync_PoliticaDesabilitada_RetornaZeroSemHttp()
    {
        var configPath = WriteMinimalConfig();
        var policy = new FakePolicyProvider(new OtaCheckPolicy(false, 30, new TimeOnly(0, 0), new TimeOnly(23, 59)));
        var httpSpy = new HttpSpyOtaClient(shouldBeCalled: false);

        try
        {
            var service = BuildService(configPath, policy, new InMemoryStatusStore(), httpSpy);
            var exitCode = await service.RunAsync(configPath);

            Assert.Equal(0, exitCode);
            Assert.False(httpSpy.WasCalled);
        }
        finally
        {
            File.Delete(configPath);
        }
    }

    [Fact]
    public async Task RunAsync_ForaDaJanela_RetornaZeroSemHttp()
    {
        var configPath = WriteMinimalConfig();
        var now = TimeOnly.FromDateTime(DateTime.Now);
        var start = now.AddHours(2);
        var end = now.AddHours(4);
        var policy = new FakePolicyProvider(new OtaCheckPolicy(true, 30, start, end));
        var httpSpy = new HttpSpyOtaClient(shouldBeCalled: false);

        try
        {
            var service = BuildService(configPath, policy, new InMemoryStatusStore(), httpSpy);
            var exitCode = await service.RunAsync(configPath);

            Assert.Equal(0, exitCode);
            Assert.False(httpSpy.WasCalled);
        }
        finally
        {
            File.Delete(configPath);
        }
    }

    [Fact]
    public async Task RunAsync_IntervaloNaoDecorrido_RetornaZeroSemHttp()
    {
        var root = Path.Combine(Path.GetTempPath(), $"ota-policy-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var configPath = WriteConfigWithKioskData(root);
        var policy = new FakePolicyProvider(OtaCheckPolicy.Default);
        var statusStore = new InMemoryStatusStore();
        var config = new JsonConfigLoader().Load(configPath);
        statusStore.SetCheckedAt(config, DateTimeOffset.UtcNow.AddMinutes(-5));
        var httpSpy = new HttpSpyOtaClient(shouldBeCalled: false);

        try
        {
            var service = BuildService(configPath, policy, statusStore, httpSpy);
            var exitCode = await service.RunAsync(configPath);

            Assert.Equal(0, exitCode);
            Assert.False(httpSpy.WasCalled);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_CheckBemSucedido_AtualizaCheckedAtMs()
    {
        var manifestPath = Path.Combine(Path.GetTempPath(), $"ota-manifest-{Guid.NewGuid():N}.json");
        var root = Path.Combine(Path.GetTempPath(), $"ota-policy-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var configPath = WriteConfigWithKioskData(root, manifestPath);

        File.WriteAllText(manifestPath, """
            {
              "app": "jukeeo",
              "version": "1.4.2",
              "arch": "aarch64",
              "sha256": "abc123",
              "signature_b64": "",
              "signature_algorithm": "rsa-pss-sha256",
              "released_at": "2026-06-12T12:00:00Z"
            }
            """);

        try
        {
            using var client = new HttpOtaUpdateClient();
            var statusStore = new FileOtaUpdateStatusStore();
            var service = new CheckUpdateService(
                new JsonConfigLoader(),
                new OtaConfigVersionSync(new JsonConfigWriter(), new FileSystemReleaseManager()),
                client,
                new ConsoleTelemetryReporter(),
                new FakePolicyProvider(OtaCheckPolicy.Default with { IntervalMinutes = 15 }),
                statusStore);

            var exitCode = await service.RunAsync(configPath);

            Assert.Equal(2, exitCode);
            var config = new JsonConfigLoader().Load(configPath);
            Assert.NotNull(statusStore.GetCheckedAt(config));
        }
        finally
        {
            File.Delete(manifestPath);
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_ComForce_IgnoraPoliticaEIntervalo()
    {
        var manifestPath = Path.Combine(Path.GetTempPath(), $"ota-manifest-{Guid.NewGuid():N}.json");
        var root = Path.Combine(Path.GetTempPath(), $"ota-policy-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var configPath = WriteConfigWithKioskData(root, manifestPath);

        File.WriteAllText(manifestPath, """
            {
              "app": "jukeeo",
              "version": "9.9.9",
              "arch": "aarch64",
              "sha256": "abc123",
              "signature_b64": "",
              "signature_algorithm": "rsa-pss-sha256",
              "released_at": "2026-06-12T12:00:00Z"
            }
            """);

        try
        {
            using var client = new HttpOtaUpdateClient();
            var statusStore = new InMemoryStatusStore();
            var config = new JsonConfigLoader().Load(configPath);
            statusStore.SetCheckedAt(config, DateTimeOffset.UtcNow);

            var now = TimeOnly.FromDateTime(DateTime.Now);
            var policy = new FakePolicyProvider(new OtaCheckPolicy(
                false,
                30,
                now.AddHours(2),
                now.AddHours(4)));

            var service = new CheckUpdateService(
                new JsonConfigLoader(),
                new OtaConfigVersionSync(new JsonConfigWriter(), new FileSystemReleaseManager()),
                client,
                new ConsoleTelemetryReporter(),
                policy,
                statusStore);

            var exitCode = await service.RunAsync(configPath, force: true);

            Assert.Equal(2, exitCode);
        }
        finally
        {
            File.Delete(manifestPath);
            Directory.Delete(root, recursive: true);
        }
    }

    private static CheckUpdateService BuildService(
        string configPath,
        IOtaPolicyProvider policy,
        IOtaUpdateStatusStore statusStore,
        HttpSpyOtaClient httpSpy) =>
        new(
            new JsonConfigLoader(),
            new OtaConfigVersionSync(new JsonConfigWriter(), new FileSystemReleaseManager()),
            httpSpy,
            new ConsoleTelemetryReporter(),
            policy,
            statusStore);

    private static string WriteMinimalConfig()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ota-config-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, """
            {
              "device_id": "machine-001",
              "ota_base_url": "file:///tmp/manifest.json",
              "current_version": "1.0.0"
            }
            """);
        return path;
    }

    private static string WriteConfigWithKioskData(string root, string? manifestPath = null)
    {
        var kioskData = Path.Combine(root, "kiosk-data");
        Directory.CreateDirectory(kioskData);
        var manifest = manifestPath ?? "file:///tmp/manifest.json";
        var fileUrl = manifestPath is not null ? new Uri(manifestPath).AbsoluteUri : manifest;
        var path = Path.Combine(root, "ota-agent.json");
        File.WriteAllText(path, $$"""
            {
              "device_id": "machine-001",
              "channel": "beta",
              "ota_base_url": "{{fileUrl}}",
              "current_version": "1.4.1",
              "public_key_path": "",
              "kiosk_data_dir": "{{kioskData.Replace("\\", "\\\\")}}"
            }
            """);
        return path;
    }

    private sealed class FakePolicyProvider(OtaCheckPolicy policy) : IOtaPolicyProvider
    {
        public OtaCheckPolicy GetPolicy(OtaAgentConfig config) => policy;
    }

    private sealed class InMemoryStatusStore : IOtaUpdateStatusStore
    {
        private readonly Dictionary<string, OtaUpdateStatus> _values = new(StringComparer.Ordinal);

        private static string Key(OtaAgentConfig config) =>
            config.KioskDataDir + "|" + config.StateDirectory;

        public OtaUpdateStatus Read(OtaAgentConfig config) =>
            _values.TryGetValue(Key(config), out var value)
                ? value
                : new OtaUpdateStatus(CurrentVersion: config.CurrentVersion);

        public void Write(OtaAgentConfig config, OtaUpdateStatus status) =>
            _values[Key(config)] = status;

        public DateTimeOffset? GetCheckedAt(OtaAgentConfig config)
        {
            var status = Read(config);
            return status.CheckedAtMs is long epochMs
                ? DateTimeOffset.FromUnixTimeMilliseconds(epochMs)
                : null;
        }

        public void SetCheckedAt(OtaAgentConfig config, DateTimeOffset timestamp) =>
            Write(config, Read(config) with { CheckedAtMs = timestamp.ToUnixTimeMilliseconds() });
    }

    private sealed class HttpSpyOtaClient(bool shouldBeCalled) : IOtaUpdateClient, IDisposable
    {
        public bool WasCalled { get; private set; }

        public Task<Domain.Entities.UpdateManifest?> CheckAsync(
            OtaAgentConfig config,
            CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            if (!shouldBeCalled)
            {
                throw new InvalidOperationException("HTTP não deveria ser invocado.");
            }

            return Task.FromResult<Domain.Entities.UpdateManifest?>(null);
        }

        public Task<string> DownloadPackageAsync(
            OtaAgentConfig config,
            Domain.Entities.UpdateManifest manifest,
            string destinationDirectory,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public void Dispose()
        {
        }
    }
}
