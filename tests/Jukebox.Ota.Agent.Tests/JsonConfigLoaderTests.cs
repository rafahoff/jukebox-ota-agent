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
              "public_key_path": "/etc/jukebox/ota-public-key.pem"
            }
            """);

        try
        {
            var loader = new JsonConfigLoader();
            var config = loader.Load(path);

            Assert.Equal("machine-001", config.DeviceId);
            Assert.Equal("beta", config.Channel);
            Assert.Equal("1.4.1", config.CurrentVersion);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
