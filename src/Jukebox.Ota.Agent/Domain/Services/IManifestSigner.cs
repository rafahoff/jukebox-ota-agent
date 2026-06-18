using Jukebox.Ota.Agent.Domain.Entities;

namespace Jukebox.Ota.Agent.Domain.Services;

/// <summary>Assina manifesto OTA com RSA-PSS (canonical JSON sem signature_b64).</summary>
public interface IManifestSigner
{
    Task<string> SignAsync(UpdateManifest manifest, string privateKeyPath, CancellationToken cancellationToken = default);
}
