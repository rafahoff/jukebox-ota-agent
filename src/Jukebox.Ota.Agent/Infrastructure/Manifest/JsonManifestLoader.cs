using System.Text.Json;
using System.Text.Json.Serialization;
using Jukebox.Ota.Agent.Domain.Entities;

namespace Jukebox.Ota.Agent.Infrastructure.Manifest;

public sealed class JsonManifestLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public UpdateManifest Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Manifesto não encontrado: {path}");
        }

        var json = File.ReadAllText(path);
        var dto = JsonSerializer.Deserialize<UpdateManifestDto>(json, Options)
            ?? throw new InvalidOperationException("Manifesto OTA inválido ou vazio.");

        return MapDto(dto);
    }

    internal static UpdateManifest FromJson(string json)
    {
        var dto = JsonSerializer.Deserialize<UpdateManifestDto>(json, Options)
            ?? throw new InvalidOperationException("Manifesto OTA inválido ou vazio.");

        return MapDto(dto);
    }

    private static UpdateManifest MapDto(UpdateManifestDto dto) =>
        new(
            dto.App ?? "jukeeo",
            dto.Version ?? throw new InvalidOperationException("version é obrigatória."),
            dto.Arch ?? "aarch64",
            dto.Sha256 ?? throw new InvalidOperationException("sha256 é obrigatório."),
            dto.SignatureB64 ?? string.Empty,
            dto.SignatureAlgorithm ?? "rsa-pss-sha256",
            dto.ReleasedAt ?? DateTimeOffset.UtcNow,
            dto.PackageType ?? "full");

    private sealed class UpdateManifestDto
    {
        [JsonPropertyName("app")]
        public string? App { get; set; }

        [JsonPropertyName("version")]
        public string? Version { get; set; }

        [JsonPropertyName("arch")]
        public string? Arch { get; set; }

        [JsonPropertyName("sha256")]
        public string? Sha256 { get; set; }

        [JsonPropertyName("signature_b64")]
        public string? SignatureB64 { get; set; }

        [JsonPropertyName("signature_algorithm")]
        public string? SignatureAlgorithm { get; set; }

        [JsonPropertyName("released_at")]
        public DateTimeOffset? ReleasedAt { get; set; }

        [JsonPropertyName("package_type")]
        public string? PackageType { get; set; }
    }
}
