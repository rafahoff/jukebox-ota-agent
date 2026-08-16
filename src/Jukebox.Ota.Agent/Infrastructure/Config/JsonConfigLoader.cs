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
            OtaPackageNaming.ResolveApp(dto.App),
            string.IsNullOrWhiteSpace(dto.Arch) ? "aarch64" : dto.Arch.Trim(),
            NormalizeKioskServiceName(dto.KioskServiceName),
            NormalizeServiceList(dto.StopServices),
            NormalizeServiceList(dto.StartServices),
            NormalizeTimerList(dto.StartTimers),
            dto.ReleasesDir ?? "/opt/jukeeo/releases",
            dto.CurrentSymlink ?? "/opt/jukeeo/current",
            dto.PreviousSymlink ?? "/opt/jukeeo/previous",
            dto.InstallRootSymlink,
            dto.BackupsDir ?? "/opt/jukeeo/backups",
            dto.HealthUrl ?? "http://127.0.0.1:8080/api/health",
            dto.HealthTimeoutSeconds ?? 90,
            dto.KioskDataDir ?? string.Empty,
            dto.BackupDataFiles,
            dto.PostInstallScript,
            dto.MaxReleaseFolders ?? 7,
            dto.StateDirectory ?? "/var/lib/jukebox-ota",
            dto.OtaCheckEnabled,
            dto.OtaCheckIntervalMinutes,
            dto.OtaCheckWindowStart,
            dto.OtaCheckWindowEnd);
    }

    internal static string NormalizeKioskServiceName(string? name)
    {
        var value = string.IsNullOrWhiteSpace(name) ? "jukeeo_kiosk_flutterpi" : name.Trim();
        return value.EndsWith(".service", StringComparison.Ordinal) ? value : value + ".service";
    }

    private static IReadOnlyList<string>? NormalizeServiceList(IReadOnlyList<string>? names)
    {
        if (names is null || names.Count == 0)
        {
            return null;
        }

        return names
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n =>
            {
                var value = n.Trim();
                return value.EndsWith(".timer", StringComparison.Ordinal) ||
                       value.EndsWith(".service", StringComparison.Ordinal)
                    ? value
                    : value + ".service";
            })
            .ToList();
    }

    private static IReadOnlyList<string>? NormalizeTimerList(IReadOnlyList<string>? names)
    {
        if (names is null || names.Count == 0)
        {
            return null;
        }

        return names
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n =>
            {
                var value = n.Trim();
                return value.EndsWith(".timer", StringComparison.Ordinal) ? value : value + ".timer";
            })
            .ToList();
    }

    private sealed class OtaAgentConfigDto
    {
        [JsonPropertyName("device_id")]
        public string? DeviceId { get; set; }

        [JsonPropertyName("channel")]
        public string? Channel { get; set; }

        [JsonPropertyName("app")]
        public string? App { get; set; }

        [JsonPropertyName("arch")]
        public string? Arch { get; set; }

        [JsonPropertyName("ota_base_url")]
        public string? OtaBaseUrl { get; set; }

        [JsonPropertyName("current_version")]
        public string? CurrentVersion { get; set; }

        [JsonPropertyName("public_key_path")]
        public string? PublicKeyPath { get; set; }

        [JsonPropertyName("kiosk_service_name")]
        public string? KioskServiceName { get; set; }

        [JsonPropertyName("stop_services")]
        public List<string>? StopServices { get; set; }

        [JsonPropertyName("start_services")]
        public List<string>? StartServices { get; set; }

        [JsonPropertyName("start_timers")]
        public List<string>? StartTimers { get; set; }

        [JsonPropertyName("releases_dir")]
        public string? ReleasesDir { get; set; }

        [JsonPropertyName("current_symlink")]
        public string? CurrentSymlink { get; set; }

        [JsonPropertyName("previous_symlink")]
        public string? PreviousSymlink { get; set; }

        [JsonPropertyName("install_root_symlink")]
        public string? InstallRootSymlink { get; set; }

        [JsonPropertyName("backups_dir")]
        public string? BackupsDir { get; set; }

        [JsonPropertyName("health_url")]
        public string? HealthUrl { get; set; }

        [JsonPropertyName("health_timeout_seconds")]
        public int? HealthTimeoutSeconds { get; set; }

        [JsonPropertyName("kiosk_data_dir")]
        public string? KioskDataDir { get; set; }

        [JsonPropertyName("backup_data_files")]
        public List<string>? BackupDataFiles { get; set; }

        [JsonPropertyName("post_install_script")]
        public string? PostInstallScript { get; set; }

        [JsonPropertyName("max_release_folders")]
        public int? MaxReleaseFolders { get; set; }

        [JsonPropertyName("state_directory")]
        public string? StateDirectory { get; set; }

        [JsonPropertyName("ota_check_enabled")]
        public bool? OtaCheckEnabled { get; set; }

        [JsonPropertyName("ota_check_interval_minutes")]
        public int? OtaCheckIntervalMinutes { get; set; }

        [JsonPropertyName("ota_check_window_start")]
        public string? OtaCheckWindowStart { get; set; }

        [JsonPropertyName("ota_check_window_end")]
        public string? OtaCheckWindowEnd { get; set; }
    }
}
