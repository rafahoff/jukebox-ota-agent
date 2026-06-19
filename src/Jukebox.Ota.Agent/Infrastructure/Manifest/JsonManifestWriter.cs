using System.Text.Json;
using System.Text.Json.Serialization;
using Jukebox.Ota.Agent.Domain.Entities;

namespace Jukebox.Ota.Agent.Infrastructure.Manifest;

public sealed class JsonManifestWriter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public void Write(string path, UpdateManifest manifest)
    {
        var dto = new UpdateManifestDto
        {
            App = manifest.App,
            Version = manifest.Version,
            Arch = manifest.Arch,
            PackageType = manifest.PackageType,
            Sha256 = manifest.Sha256.ToLowerInvariant(),
            SignatureB64 = string.IsNullOrWhiteSpace(manifest.SignatureB64) ? null : manifest.SignatureB64,
            SignatureAlgorithm = manifest.SignatureAlgorithm,
            ReleasedAt = manifest.ReleasedAt.ToUniversalTime(),
            DownloadUrl = manifest.DownloadUrl,
        };

        var json = JsonSerializer.Serialize(dto, Options);
        File.WriteAllText(path, json);
    }

    private sealed class UpdateManifestDto
    {
        [JsonPropertyName("app")]
        public string App { get; set; } = string.Empty;

        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;

        [JsonPropertyName("arch")]
        public string Arch { get; set; } = string.Empty;

        [JsonPropertyName("package_type")]
        public string PackageType { get; set; } = "full";

        [JsonPropertyName("sha256")]
        public string Sha256 { get; set; } = string.Empty;

        [JsonPropertyName("signature_b64")]
        public string? SignatureB64 { get; set; }

        [JsonPropertyName("signature_algorithm")]
        public string SignatureAlgorithm { get; set; } = "rsa-pss-sha256";

        [JsonPropertyName("released_at")]
        public DateTimeOffset ReleasedAt { get; set; }

        [JsonPropertyName("download_url")]
        public string? DownloadUrl { get; set; }
    }
}
