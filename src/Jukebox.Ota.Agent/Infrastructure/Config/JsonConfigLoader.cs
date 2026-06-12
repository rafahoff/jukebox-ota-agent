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
            dto.PublicKeyPath ?? string.Empty);
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
    }
}
