using Jukebox.Ota.Agent.Domain.Services;
using Jukebox.Ota.Agent.Infrastructure.Logging;
using Jukebox.Ota.Agent.Infrastructure.Manifest;

namespace Jukebox.Ota.Agent.Application.Services;

public sealed class SignManifestService
{
    private readonly JsonManifestLoader _manifestLoader;
    private readonly JsonManifestWriter _manifestWriter;
    private readonly IManifestSigner _signer;

    public SignManifestService(
        JsonManifestLoader manifestLoader,
        JsonManifestWriter manifestWriter,
        IManifestSigner signer)
    {
        _manifestLoader = manifestLoader;
        _manifestWriter = manifestWriter;
        _signer = signer;
    }

    public async Task<int> RunAsync(
        string manifestPath,
        string privateKeyPath,
        string? outputPath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var manifest = _manifestLoader.Load(manifestPath);
            var signature = await _signer.SignAsync(manifest, privateKeyPath, cancellationToken);
            var signed = manifest with
            {
                SignatureB64 = signature,
                SignatureAlgorithm = "rsa-pss-sha256",
            };

            var destination = outputPath ?? manifestPath;
            _manifestWriter.Write(destination, signed);
            Console.WriteLine($"Manifesto assinado gravado em: {destination}");
            FileAgentLogger.LogSignManifest($"Manifesto assinado gravado em: {destination}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"sign-manifest falhou: {ex.Message}");
            FileAgentLogger.LogSignManifest($"falhou: {ex.Message}");
            return 1;
        }
    }
}
