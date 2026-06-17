using System.Security.Cryptography;
using System.Text;
using Jukebox.Ota.Agent.Domain.Entities;
using Jukebox.Ota.Agent.Infrastructure.Security;

namespace Jukebox.Ota.Agent.Tests;

public class RsaPssPackageVerifierTests
{
    [Fact]
    public async Task VerifyAsync_Sha256Invalido_RetornaFalha()
    {
        var packagePath = await CreateTempFileAsync("conteudo-do-pacote");
        var manifest = new UpdateManifest(
            "jukebox_tv",
            "1.4.2",
            "aarch64",
            "0000000000000000000000000000000000000000000000000000000000000000",
            string.Empty,
            "rsa-pss-sha256",
            DateTimeOffset.Parse("2026-06-12T12:00:00Z"));

        try
        {
            var verifier = new RsaPssPackageVerifier();
            var result = await verifier.VerifyAsync(manifest, packagePath, string.Empty);

            Assert.False(result.Success);
            Assert.Contains("SHA-256", result.Message);
        }
        finally
        {
            File.Delete(packagePath);
        }
    }

    [Fact]
    public async Task VerifyAsync_Sha256ValidoSemAssinatura_RetornaSucesso()
    {
        var packagePath = await CreateTempFileAsync("conteudo-do-pacote");
        var hash = await Sha256Hasher.ComputeHexAsync(packagePath);
        var manifest = new UpdateManifest(
            "jukebox_tv",
            "1.4.2",
            "aarch64",
            hash,
            string.Empty,
            "rsa-pss-sha256",
            DateTimeOffset.Parse("2026-06-12T12:00:00Z"));

        try
        {
            var verifier = new RsaPssPackageVerifier();
            var result = await verifier.VerifyAsync(manifest, packagePath, string.Empty);

            Assert.True(result.Success);
        }
        finally
        {
            File.Delete(packagePath);
        }
    }

    [Fact]
    public async Task VerifyAsync_AssinaturaInvalida_RetornaFalha()
    {
        using var rsa = RSA.Create(2048);
        var publicPem = rsa.ExportRSAPublicKeyPem();

        var packagePath = await CreateTempFileAsync("pacote-com-assinatura-invalida");
        var hash = await Sha256Hasher.ComputeHexAsync(packagePath);
        var manifest = new UpdateManifest(
            "jukebox_tv",
            "1.4.2",
            "aarch64",
            hash,
            Convert.ToBase64String(new byte[256]),
            "rsa-pss-sha256",
            DateTimeOffset.Parse("2026-06-12T12:00:00Z"));

        var publicKeyPath = Path.Combine(Path.GetTempPath(), $"ota-pub-{Guid.NewGuid():N}.pem");
        await File.WriteAllTextAsync(publicKeyPath, publicPem);

        try
        {
            var verifier = new RsaPssPackageVerifier();
            var result = await verifier.VerifyAsync(manifest, packagePath, publicKeyPath);

            Assert.False(result.Success);
            Assert.Contains("Assinatura", result.Message);
        }
        finally
        {
            File.Delete(packagePath);
            File.Delete(publicKeyPath);
        }
    }

    [Fact]
    public async Task VerifyAsync_PacoteAlteradoAposHash_RetornaFalha()
    {
        var packagePath = await CreateTempFileAsync("conteudo-original");
        var hash = await Sha256Hasher.ComputeHexAsync(packagePath);
        await File.WriteAllTextAsync(packagePath, "conteudo-tamperado");

        var manifest = new UpdateManifest(
            "jukebox_tv",
            "1.4.2",
            "aarch64",
            hash,
            string.Empty,
            "rsa-pss-sha256",
            DateTimeOffset.Parse("2026-06-12T12:00:00Z"));

        try
        {
            var verifier = new RsaPssPackageVerifier();
            var result = await verifier.VerifyAsync(manifest, packagePath, string.Empty);

            Assert.False(result.Success);
            Assert.Contains("SHA-256", result.Message);
        }
        finally
        {
            File.Delete(packagePath);
        }
    }

    [Fact]
    public async Task VerifyAsync_AssinaturaValida_RetornaSucesso()
    {
        using var rsa = RSA.Create(2048);
        var privatePem = rsa.ExportRSAPrivateKeyPem();
        var publicPem = rsa.ExportRSAPublicKeyPem();

        var packagePath = await CreateTempFileAsync("pacote-assinado");
        var hash = await Sha256Hasher.ComputeHexAsync(packagePath);
        var manifest = new UpdateManifest(
            "jukebox_tv",
            "1.4.2",
            "aarch64",
            hash,
            string.Empty,
            "rsa-pss-sha256",
            DateTimeOffset.Parse("2026-06-12T12:00:00Z"));

        var canonical = RsaPssPackageVerifier.BuildCanonicalManifestJson(manifest);
        var signature = rsa.SignData(
            Encoding.UTF8.GetBytes(canonical),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pss);

        manifest = manifest with { SignatureB64 = Convert.ToBase64String(signature) };

        var publicKeyPath = Path.Combine(Path.GetTempPath(), $"ota-pub-{Guid.NewGuid():N}.pem");
        await File.WriteAllTextAsync(publicKeyPath, publicPem);

        try
        {
            var verifier = new RsaPssPackageVerifier();
            var result = await verifier.VerifyAsync(manifest, packagePath, publicKeyPath);

            Assert.True(result.Success);
        }
        finally
        {
            File.Delete(packagePath);
            File.Delete(publicKeyPath);
        }
    }

    private static async Task<string> CreateTempFileAsync(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"ota-pkg-{Guid.NewGuid():N}.bin");
        await File.WriteAllTextAsync(path, content);
        return path;
    }
}
