using System.Text.Json;
using System.Text.Json.Serialization;
using Jukebox.Ota.Agent.Domain.ValueObjects;

namespace Jukebox.Ota.Agent.Infrastructure.Config;

public sealed class JsonConfigLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public OtaAgentConfig Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Arquivo de configuração não encontrado: {path}");
        }

        var json = File.ReadAllText(path);
        var dto = JsonSerializer.Deserialize<OtaAgentConfigDto>(json, Options)
            ?? throw new InvalidOperationException("Configuração OTA inválida ou vazia.");

        return new OtaAgentConfig(
            dto.DeviceId ?? throw new InvalidOperationException("device_id é obrigatório."),
            dto.Channel ?? "stable",
            dto.OtaBaseUrl ?? throw new InvalidOperationException("ota_base_url é obrigatório."),
            dto.CurrentVersion ?? "0.0.0",
            dto.PublicKeyPath ?? string.Empty,
            NormalizeKioskServiceName(dto.KioskServiceName),
            dto.ReleasesDir ?? "/opt/jukeeo/releases",
            dto.CurrentSymlink ?? "/opt/jukeeo/current",
            dto.PreviousSymlink ?? "/opt/jukeeo/previous",
            dto.BackupsDir ?? "/opt/jukeeo/backups",
            dto.HealthUrl ?? "http://127.0.0.1:8080/api/health",
            dto.KioskDataDir ?? string.Empty,
            dto.MaxReleaseFolders ?? 7);
    }

    internal static string NormalizeKioskServiceName(string? name)
    {
        var value = string.IsNullOrWhiteSpace(name) ? "jukeeo_kiosk_flutterpi" : name.Trim();
        return value.EndsWith(".service", StringComparison.Ordinal) ? value : value + ".service";
    }

    private sealed class OtaAgentConfigDto
    {
        [JsonPropertyName("device_id")]
        public string? DeviceId { get; set; }

        [JsonPropertyName("channel")]
        public string? Channel { get; set; }

        [JsonPropertyName("ota_base_url")]
        public string? OtaBaseUrl { get; set; }

        [JsonPropertyName("current_version")]
        public string? CurrentVersion { get; set; }

        [JsonPropertyName("public_key_path")]
        public string? PublicKeyPath { get; set; }

        [JsonPropertyName("kiosk_service_name")]
        public string? KioskServiceName { get; set; }

        [JsonPropertyName("releases_dir")]
        public string? ReleasesDir { get; set; }

        [JsonPropertyName("current_symlink")]
        public string? CurrentSymlink { get; set; }

        [JsonPropertyName("previous_symlink")]
        public string? PreviousSymlink { get; set; }

        [JsonPropertyName("backups_dir")]
        public string? BackupsDir { get; set; }

        [JsonPropertyName("health_url")]
        public string? HealthUrl { get; set; }

        [JsonPropertyName("kiosk_data_dir")]
        public string? KioskDataDir { get; set; }

        [JsonPropertyName("max_release_folders")]
        public int? MaxReleaseFolders { get; set; }
    }
}
