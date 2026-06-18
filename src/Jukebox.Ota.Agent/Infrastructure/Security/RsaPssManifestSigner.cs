using System.Security.Cryptography;
using System.Text;
using Jukebox.Ota.Agent.Domain.Entities;
using Jukebox.Ota.Agent.Domain.Services;

namespace Jukebox.Ota.Agent.Infrastructure.Security;

public sealed class RsaPssManifestSigner : IManifestSigner
{
    public async Task<string> SignAsync(
        UpdateManifest manifest,
        string privateKeyPath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(privateKeyPath))
        {
            throw new FileNotFoundException($"Chave privada não encontrada: {privateKeyPath}");
        }

        var canonical = RsaPssPackageVerifier.BuildCanonicalManifestJson(manifest);
        var payload = Encoding.UTF8.GetBytes(canonical);

        using var rsa = RSA.Create();
        rsa.ImportFromPem(await File.ReadAllTextAsync(privateKeyPath, cancellationToken));

        var signature = rsa.SignData(payload, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        return Convert.ToBase64String(signature);
    }
}
