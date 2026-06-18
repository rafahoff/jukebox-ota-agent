using Jukebox.Ota.Agent.Infrastructure.Config;

namespace Jukebox.Ota.Agent.Tests;

public class JsonConfigLoaderTests
{
    [Fact]
    public void Load_LeConfiguracaoSnakeCase()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ota-config-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, """
            {
              "device_id": "machine-001",
              "channel": "beta",
              "ota_base_url": "file:///tmp/manifest.json",
              "current_version": "1.4.1",
              "public_key_path": "/etc/jukeeo/ota-public-key.pem"
            }
            """);

        try
        {
            var loader = new JsonConfigLoader();
            var config = loader.Load(path);

            Assert.Equal("machine-001", config.DeviceId);
            Assert.Equal("beta", config.Channel);
            Assert.Equal("1.4.1", config.CurrentVersion);
            Assert.Equal("jukeeo_kiosk_flutterpi.service", config.KioskServiceName);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("jukeeo_kiosk_flutterpi", "jukeeo_kiosk_flutterpi.service")]
    [InlineData("jukeeo_kiosk_flutterpi.service", "jukeeo_kiosk_flutterpi.service")]
    public void Load_NormalizaKioskServiceName(string input, string expected)
    {
        var path = Path.Combine(Path.GetTempPath(), $"ota-config-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, $$"""
            {
              "device_id": "machine-001",
              "ota_base_url": "file:///tmp/manifest.json",
              "kiosk_service_name": "{{input}}"
            }
            """);

        try
        {
            var config = new JsonConfigLoader().Load(path);
            Assert.Equal(expected, config.KioskServiceName);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_SemKioskServiceName_UsaPadraoComService()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ota-config-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, """
            {
              "device_id": "machine-001",
              "ota_base_url": "file:///tmp/manifest.json"
            }
            """);

        try
        {
            var config = new JsonConfigLoader().Load(path);
            Assert.Equal("jukeeo_kiosk_flutterpi.service", config.KioskServiceName);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
