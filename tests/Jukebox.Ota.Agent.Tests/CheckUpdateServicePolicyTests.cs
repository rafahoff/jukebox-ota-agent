using Jukebox.Ota.Agent.Application.Services;
using Jukebox.Ota.Agent.Domain.Repositories;
using Jukebox.Ota.Agent.Domain.Services;
using Jukebox.Ota.Agent.Domain.ValueObjects;
using Jukebox.Ota.Agent.Infrastructure.Config;
using Jukebox.Ota.Agent.Infrastructure.ExternalServices;
using Jukebox.Ota.Agent.Infrastructure.Policy;
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
            var service = BuildService(configPath, policy, new InMemoryCheckStateStore(), httpSpy);
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
            var service = BuildService(configPath, policy, new InMemoryCheckStateStore(), httpSpy);
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
        var configPath = WriteMinimalConfig();
        var policy = new FakePolicyProvider(OtaCheckPolicy.Default);
        var stateStore = new InMemoryCheckStateStore();
        stateStore.SetLastCheckAt("/var/lib/jukebox-ota", DateTimeOffset.UtcNow.AddMinutes(-5));
        var httpSpy = new HttpSpyOtaClient(shouldBeCalled: false);

        try
        {
            var service = BuildService(configPath, policy, stateStore, httpSpy);
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
    public async Task RunAsync_CheckBemSucedido_AtualizaLastCheckAt()
    {
        var manifestPath = Path.Combine(Path.GetTempPath(), $"ota-manifest-{Guid.NewGuid():N}.json");
        var configPath = Path.Combine(Path.GetTempPath(), $"ota-config-{Guid.NewGuid():N}.json");
        var stateDir = Path.Combine(Path.GetTempPath(), $"ota-state-{Guid.NewGuid():N}");

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

        var fileUrl = new Uri(manifestPath).AbsoluteUri;
        File.WriteAllText(configPath, $$"""
            {
              "device_id": "machine-001",
              "channel": "beta",
              "ota_base_url": "{{fileUrl}}",
              "current_version": "1.4.1",
              "public_key_path": "",
              "state_directory": "{{stateDir.Replace("\\", "\\\\")}}"
            }
            """);

        try
        {
            using var client = new HttpOtaUpdateClient();
            var stateStore = new FileOtaCheckStateStore();
            var service = new CheckUpdateService(
                new JsonConfigLoader(),
                client,
                new ConsoleTelemetryReporter(),
                new FakePolicyProvider(OtaCheckPolicy.Default with { IntervalMinutes = 15 }),
                stateStore);

            var exitCode = await service.RunAsync(configPath);

            Assert.Equal(2, exitCode);
            Assert.NotNull(stateStore.GetLastCheckAt(stateDir));
        }
        finally
        {
            File.Delete(manifestPath);
            File.Delete(configPath);
            if (Directory.Exists(stateDir))
            {
                Directory.Delete(stateDir, recursive: true);
            }
        }
    }

    private static CheckUpdateService BuildService(
        string configPath,
        IOtaPolicyProvider policy,
        IOtaCheckStateStore stateStore,
        HttpSpyOtaClient httpSpy) =>
        new(
            new JsonConfigLoader(),
            httpSpy,
            new ConsoleTelemetryReporter(),
            policy,
            stateStore);

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

    private sealed class FakePolicyProvider(OtaCheckPolicy policy) : IOtaPolicyProvider
    {
        public OtaCheckPolicy GetPolicy(OtaAgentConfig config) => policy;
    }

    private sealed class InMemoryCheckStateStore : IOtaCheckStateStore
    {
        private readonly Dictionary<string, DateTimeOffset> _values = new(StringComparer.Ordinal);

        public DateTimeOffset? GetLastCheckAt(string stateDirectory) =>
            _values.TryGetValue(stateDirectory, out var value) ? value : null;

        public void SetLastCheckAt(string stateDirectory, DateTimeOffset timestamp) =>
            _values[stateDirectory] = timestamp;
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

        public void Dispose()
        {
        }
    }
}
