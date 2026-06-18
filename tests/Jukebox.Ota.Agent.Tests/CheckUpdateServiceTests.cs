using Jukebox.Ota.Agent.Application.Services;
using Jukebox.Ota.Agent.Infrastructure.Config;
using Jukebox.Ota.Agent.Infrastructure.ExternalServices;
using Jukebox.Ota.Agent.Infrastructure.Telemetry;

namespace Jukebox.Ota.Agent.Tests;

public class CheckUpdateServiceTests
{
    [Fact]
    public async Task RunAsync_ComFixtureLocal_RetornaAtualizacaoDisponivel()
    {
        var manifestPath = Path.Combine(Path.GetTempPath(), $"ota-manifest-{Guid.NewGuid():N}.json");
        var configPath = Path.Combine(Path.GetTempPath(), $"ota-config-{Guid.NewGuid():N}.json");

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
              "public_key_path": ""
            }
            """);

        try
        {
            using var client = new HttpOtaUpdateClient();
            var service = new CheckUpdateService(
                new JsonConfigLoader(),
                client,
                new ConsoleTelemetryReporter());

            var exitCode = await service.RunAsync(configPath);

            Assert.Equal(2, exitCode);
        }
        finally
        {
            File.Delete(manifestPath);
            File.Delete(configPath);
        }
    }
}
