namespace Jukebox.Ota.Agent.Domain.Entities;

/// <summary>Manifesto de release OTA recebido do servidor ou fixture local.</summary>
public sealed record UpdateManifest(
    string App,
    string Version,
    string Arch,
    string Sha256,
    string SignatureB64,
    string SignatureAlgorithm,
    DateTimeOffset ReleasedAt,
    string PackageType = "full",
    string? DownloadUrl = null
);
