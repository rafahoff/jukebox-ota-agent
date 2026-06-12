using Jukebox.Ota.Agent.Domain.Entities;

namespace Jukebox.Ota.Agent.Domain.Services;

/// <summary>Verifica integridade e assinatura de pacote OTA.</summary>
public interface IPackageVerifier
{
  Task<PackageVerificationResult> VerifyAsync(
      UpdateManifest manifest,
      string packagePath,
      string publicKeyPath,
      CancellationToken cancellationToken = default);
}

public sealed record PackageVerificationResult(bool Success, string Message);
