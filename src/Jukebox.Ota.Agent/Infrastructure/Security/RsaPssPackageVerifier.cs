using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Jukebox.Ota.Agent.Domain.Entities;
using Jukebox.Ota.Agent.Domain.Services;

namespace Jukebox.Ota.Agent.Infrastructure.Security;

/// <summary>Verifica SHA-256 do pacote e assinatura RSA-PSS do manifesto (canonical JSON).</summary>
public sealed class RsaPssPackageVerifier : IPackageVerifier
{
    public async Task<PackageVerificationResult> VerifyAsync(
        UpdateManifest manifest,
        string packagePath,
        string publicKeyPath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(packagePath))
        {
            return new PackageVerificationResult(false, $"Pacote não encontrado: {packagePath}");
        }

        var actualHash = await Sha256Hasher.ComputeHexAsync(packagePath, cancellationToken);
        if (!string.Equals(actualHash, manifest.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            return new PackageVerificationResult(false, "SHA-256 do pacote não confere com o manifesto.");
        }

        if (string.IsNullOrWhiteSpace(manifest.SignatureB64))
        {
            return new PackageVerificationResult(true, "SHA-256 válido; assinatura ausente no manifesto (aceito na POC).");
        }

        if (string.IsNullOrWhiteSpace(publicKeyPath) || !File.Exists(publicKeyPath))
        {
            return new PackageVerificationResult(false, "Chave pública não configurada ou inexistente.");
        }

        if (!string.Equals(manifest.SignatureAlgorithm, "rsa-pss-sha256", StringComparison.OrdinalIgnoreCase))
        {
            return new PackageVerificationResult(false, $"Algoritmo não suportado: {manifest.SignatureAlgorithm}");
        }

        var canonical = BuildCanonicalManifestJson(manifest);
        var payload = Encoding.UTF8.GetBytes(canonical);
        var signature = Convert.FromBase64String(manifest.SignatureB64);

        using var rsa = RSA.Create();
        rsa.ImportFromPem(await File.ReadAllTextAsync(publicKeyPath, cancellationToken));

        var valid = rsa.VerifyData(
            payload,
            signature,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pss);

        return valid
            ? new PackageVerificationResult(true, "Pacote e assinatura do manifesto válidos.")
            : new PackageVerificationResult(false, "Assinatura do manifesto inválida.");
    }

    public static string BuildCanonicalManifestJson(UpdateManifest manifest)
    {
        var dto = new
        {
            app = manifest.App,
            version = manifest.Version,
            arch = manifest.Arch,
            package_type = manifest.PackageType,
            sha256 = manifest.Sha256.ToLowerInvariant(),
            signature_algorithm = manifest.SignatureAlgorithm,
            released_at = manifest.ReleasedAt.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"),
        };

        return JsonSerializer.Serialize(dto);
    }
}
