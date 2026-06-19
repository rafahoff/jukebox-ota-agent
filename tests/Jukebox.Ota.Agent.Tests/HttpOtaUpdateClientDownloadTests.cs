using Jukebox.Ota.Agent.Application.Services;
using Jukebox.Ota.Agent.Domain.Repositories;
using Jukebox.Ota.Agent.Domain.Services;
using Jukebox.Ota.Agent.Domain.ValueObjects;
using Jukebox.Ota.Agent.Infrastructure.Config;
using Jukebox.Ota.Agent.Infrastructure.ExternalServices;
using Jukebox.Ota.Agent.Infrastructure.Policy;
using Jukebox.Ota.Agent.Infrastructure.Telemetry;

namespace Jukebox.Ota.Agent.Tests;

public class HttpOtaUpdateClientDownloadTests
{
    [Fact]
    public void ResolveDownloadUrl_ComFixtureFile_UsaPacoteNoMesmoDiretorio()
    {
        var manifestPath = @"C:\ota\manifest.json";
        var config = new OtaAgentConfig(
            "machine-test",
            "beta",
            new Uri(manifestPath).AbsoluteUri,
            "1.0.0",
            string.Empty);
        var manifest = new Domain.Entities.UpdateManifest(
            "jukeeo",
            "1.4.2",
            "aarch64",
            "abc",
            string.Empty,
            "rsa-pss-sha256",
            DateTimeOffset.UtcNow);

        var url = HttpOtaUpdateClient.ResolveDownloadUrl(config, manifest);

        Assert.EndsWith("jukeeo-1.4.2+aarch64.tar.zst", url, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DownloadPackageAsync_ComFileLocal_CopiaPacote()
    {
        var root = Path.Combine(Path.GetTempPath(), $"ota-download-{Guid.NewGuid():N}");
        var packageSource = Path.Combine(root, "jukeeo-1.4.2+aarch64.tar.zst");
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(packageSource, "pacote");

        var manifestPath = Path.Combine(root, "manifest.json");
        var config = new OtaAgentConfig(
            "machine-test",
            "beta",
            new Uri(manifestPath).AbsoluteUri,
            "1.0.0",
            string.Empty);
        var manifest = new Domain.Entities.UpdateManifest(
            "jukeeo",
            "1.4.2",
            "aarch64",
            "abc",
            string.Empty,
            "rsa-pss-sha256",
            DateTimeOffset.UtcNow);

        try
        {
            using var client = new HttpOtaUpdateClient();
            var destinationDir = Path.Combine(root, "downloads");
            var downloaded = await client.DownloadPackageAsync(config, manifest, destinationDir);

            Assert.True(File.Exists(downloaded));
            Assert.Equal("pacote", await File.ReadAllTextAsync(downloaded));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
