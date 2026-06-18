using System.Security.Cryptography;
using System.Text;
using Jukebox.Ota.Agent.Application.Services;
using Jukebox.Ota.Agent.Domain.Entities;
using Jukebox.Ota.Agent.Infrastructure.Manifest;
using Jukebox.Ota.Agent.Infrastructure.Security;

namespace Jukebox.Ota.Agent.Tests;

public class SignManifestServiceTests
{
    [Fact]
    public async Task RunAsync_AssinaManifesto_GravaSignatureB64()
    {
        using var rsa = RSA.Create(2048);
        var privateKeyPath = Path.Combine(Path.GetTempPath(), $"ota-priv-{Guid.NewGuid():N}.pem");
        var packagePath = Path.Combine(Path.GetTempPath(), $"ota-pkg-{Guid.NewGuid():N}.bin");
        var manifestPath = Path.Combine(Path.GetTempPath(), $"ota-manifest-{Guid.NewGuid():N}.json");
        var outputPath = Path.Combine(Path.GetTempPath(), $"ota-signed-{Guid.NewGuid():N}.json");

        await File.WriteAllTextAsync(packagePath, "conteudo-do-pacote");
        var hash = await Sha256Hasher.ComputeHexAsync(packagePath);
        await File.WriteAllTextAsync(privateKeyPath, rsa.ExportRSAPrivateKeyPem());
        File.WriteAllText(manifestPath, $$"""
            {
              "app": "jukeeo",
              "version": "1.4.2",
              "arch": "aarch64",
              "package_type": "full",
              "sha256": "{{hash}}",
              "released_at": "2026-06-12T12:00:00Z"
            }
            """);

        try
        {
            var service = new SignManifestService(
                new JsonManifestLoader(),
                new JsonManifestWriter(),
                new RsaPssManifestSigner());

            var exitCode = await service.RunAsync(manifestPath, privateKeyPath, outputPath);

            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(outputPath));

            var signed = new JsonManifestLoader().Load(outputPath);
            Assert.False(string.IsNullOrWhiteSpace(signed.SignatureB64));
            Assert.Equal("rsa-pss-sha256", signed.SignatureAlgorithm);

            var publicKeyPath = Path.Combine(Path.GetTempPath(), $"ota-pub-{Guid.NewGuid():N}.pem");
            await File.WriteAllTextAsync(publicKeyPath, rsa.ExportRSAPublicKeyPem());

            try
            {
                var verifier = new RsaPssPackageVerifier();
                var result = await verifier.VerifyAsync(signed, packagePath, publicKeyPath);
                Assert.True(result.Success);
            }
            finally
            {
                File.Delete(publicKeyPath);
            }
        }
        finally
        {
            File.Delete(privateKeyPath);
            File.Delete(packagePath);
            File.Delete(manifestPath);
            File.Delete(outputPath);
        }
    }

    [Fact]
    public async Task BuildCanonical_IncluiPackageType()
    {
        var manifest = new UpdateManifest(
            "jukeeo",
            "1.4.2",
            "aarch64",
            "abc123",
            string.Empty,
            "rsa-pss-sha256",
            DateTimeOffset.Parse("2026-06-12T12:00:00Z"),
            "full");

        var canonical = RsaPssPackageVerifier.BuildCanonicalManifestJson(manifest);

        Assert.Contains("\"package_type\":\"full\"", canonical);
        Assert.DoesNotContain("signature_b64", canonical);
    }
}
