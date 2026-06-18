using Jukebox.Ota.Agent.Infrastructure.Manifest;

namespace Jukebox.Ota.Agent.Tests;

public class JsonManifestLoaderTests
{
    [Fact]
    public void Load_SemCampoApp_DefaultJukeeo()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ota-manifest-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, """
            {
              "version": "1.0.0",
              "sha256": "abc"
            }
            """);

        try
        {
            var manifest = new JsonManifestLoader().Load(path);
            Assert.Equal("jukeeo", manifest.App);
            Assert.Equal("full", manifest.PackageType);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
