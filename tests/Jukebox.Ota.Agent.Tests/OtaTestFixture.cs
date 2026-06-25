using System.Security.Cryptography;
using System.Text;
using Jukebox.Ota.Agent.Infrastructure.Security;

namespace Jukebox.Ota.Agent.Tests;

/// <summary>Fixture local file:// com manifesto + pacote .tar.zst para testes de check/download.</summary>
internal static class OtaTestFixture
{
    public static async Task<OtaFixturePaths> CreateAsync(
        string root,
        string remoteVersion = "1.4.2",
        string currentVersion = "1.4.1",
        string packageContent = "conteudo-pacote-ota-teste")
    {
        var otaDir = Path.Combine(root, "ota-fixture");
        Directory.CreateDirectory(otaDir);

        var packageName = $"jukeeo-{remoteVersion}+aarch64.tar.zst";
        var packagePath = Path.Combine(otaDir, packageName);
        await File.WriteAllTextAsync(packagePath, packageContent);

        var sha256 = await Sha256Hasher.ComputeHexAsync(packagePath);
        var manifestPath = Path.Combine(otaDir, "manifest.json");
        await File.WriteAllTextAsync(manifestPath, $$"""
            {
              "app": "jukeeo",
              "version": "{{remoteVersion}}",
              "arch": "aarch64",
              "sha256": "{{sha256}}",
              "signature_b64": "",
              "signature_algorithm": "rsa-pss-sha256",
              "released_at": "2026-06-12T12:00:00Z"
            }
            """);

        var kioskData = Path.Combine(root, "kiosk-data");
        Directory.CreateDirectory(kioskData);
        var stateDir = Path.Combine(root, "state");
        Directory.CreateDirectory(stateDir);

        var configPath = Path.Combine(root, "ota-agent.json");
        var fileUrl = new Uri(manifestPath).AbsoluteUri;
        await File.WriteAllTextAsync(configPath, $$"""
            {
              "device_id": "machine-001",
              "channel": "beta",
              "ota_base_url": "{{fileUrl}}",
              "current_version": "{{currentVersion}}",
              "public_key_path": "",
              "kiosk_data_dir": "{{kioskData.Replace("\\", "\\\\")}}",
              "state_directory": "{{stateDir.Replace("\\", "\\\\")}}"
            }
            """);

        return new OtaFixturePaths(root, configPath, manifestPath, packagePath, kioskData, stateDir, remoteVersion);
    }

    internal sealed record OtaFixturePaths(
        string Root,
        string ConfigPath,
        string ManifestPath,
        string PackageSourcePath,
        string KioskDataDir,
        string StateDirectory,
        string RemoteVersion);
}
