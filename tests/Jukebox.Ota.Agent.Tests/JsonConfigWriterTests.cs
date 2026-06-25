using Jukebox.Ota.Agent.Application.Services;
using Jukebox.Ota.Agent.Domain.ValueObjects;
using Jukebox.Ota.Agent.Infrastructure.Config;
using Jukebox.Ota.Agent.Infrastructure.Release;

namespace Jukebox.Ota.Agent.Tests;

public class JsonConfigWriterTests
{
    [Fact]
    public void UpdateCurrentVersion_AlteraApenasCampoVersion()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ota-config-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, """
            {
              "device_id": "machine-001",
              "ota_base_url": "file:///tmp/manifest.json",
              "current_version": "1.0.15",
              "channel": "stable"
            }
            """);

        try
        {
            new JsonConfigWriter().UpdateCurrentVersion(path, "1.0.16");

            var json = File.ReadAllText(path);
            Assert.Contains("\"current_version\": \"1.0.16\"", json);
            Assert.Contains("\"device_id\": \"machine-001\"", json);
            Assert.Contains("\"channel\": \"stable\"", json);

            var config = new JsonConfigLoader().Load(path);
            Assert.Equal("1.0.16", config.CurrentVersion);
        }
        finally
        {
            File.Delete(path);
        }
    }
}

public class OtaConfigVersionSyncTests
{
    [Fact]
    public void ResolveAndSync_QuandoReleaseDiverge_AtualizaConfig()
    {
        var root = Path.Combine(Path.GetTempPath(), $"ota-sync-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var releasesDir = Path.Combine(root, "releases");
        var releaseDir = Path.Combine(releasesDir, "1.0.16+aarch64");
        Directory.CreateDirectory(releaseDir);
        var currentLink = Path.Combine(root, "current");
        Directory.CreateSymbolicLink(currentLink, releaseDir);

        var configPath = Path.Combine(root, "ota-agent.json");
        File.WriteAllText(configPath, """
            {
              "device_id": "machine-001",
              "ota_base_url": "file:///tmp/manifest.json",
              "current_version": "1.0.15",
              "releases_dir": "RELEASES",
              "current_symlink": "CURRENT"
            }
            """.Replace("RELEASES", releasesDir.Replace("\\", "\\\\"))
               .Replace("CURRENT", currentLink.Replace("\\", "\\\\")));

        try
        {
            var loader = new JsonConfigLoader();
            var config = loader.Load(configPath);
            var sync = new OtaConfigVersionSync(new JsonConfigWriter(), new FileSystemReleaseManager());

            var resolved = sync.ResolveAndSync(configPath, config);

            Assert.Equal("1.0.16", resolved.CurrentVersion);
            Assert.Equal("1.0.16", loader.Load(configPath).CurrentVersion);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ResolveAndSync_QuandoCoincide_NaoAlteraConfig()
    {
        var root = Path.Combine(Path.GetTempPath(), $"ota-sync-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var configPath = Path.Combine(root, "ota-agent.json");
        File.WriteAllText(configPath, """
            {
              "device_id": "machine-001",
              "ota_base_url": "file:///tmp/manifest.json",
              "current_version": "1.0.16"
            }
            """);

        try
        {
            var loader = new JsonConfigLoader();
            var config = loader.Load(configPath);
            var sync = new OtaConfigVersionSync(new JsonConfigWriter(), new FileSystemReleaseManager());

            var resolved = sync.ResolveAndSync(configPath, config);

            Assert.Equal("1.0.16", resolved.CurrentVersion);
            Assert.Equal("1.0.16", loader.Load(configPath).CurrentVersion);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
