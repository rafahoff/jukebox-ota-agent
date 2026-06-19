using System.Text.Json;
using System.Text.Json.Serialization;
using Jukebox.Ota.Agent.Domain.Services;
using Jukebox.Ota.Agent.Domain.ValueObjects;
using Jukebox.Ota.Agent.Infrastructure.Backup;

namespace Jukebox.Ota.Agent.Infrastructure.Policy;

/// <summary>Persiste <c>ota_update_status.json</c> com writes atómicos e migração one-shot de <c>last_check_at_ms</c>.</summary>
public sealed class FileOtaUpdateStatusStore : IOtaUpdateStatusStore
{
    private const string StatusFileName = "ota_update_status.json";
    private const string LegacyStateFileName = "last_check_at_ms";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
    };

    public OtaUpdateStatus Read(OtaAgentConfig config)
    {
        var status = ReadRaw(config);
        return MigrateLegacyCheckedAtIfNeeded(config, status);
    }

    public void Write(OtaAgentConfig config, OtaUpdateStatus status)
    {
        var path = GetStatusFilePath(config);
        var directory = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException($"Caminho de estado inválido: {path}");

        Directory.CreateDirectory(directory);

        var dto = MapToDto(status);
        var json = JsonSerializer.Serialize(dto, Options);
        var tempPath = path + $".{Guid.NewGuid():N}.tmp";

        try
        {
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    public DateTimeOffset? GetCheckedAt(OtaAgentConfig config)
    {
        var status = Read(config);
        return status.CheckedAtMs is long epochMs
            ? DateTimeOffset.FromUnixTimeMilliseconds(epochMs)
            : null;
    }

    public void SetCheckedAt(OtaAgentConfig config, DateTimeOffset timestamp)
    {
        var current = Read(config);
        Write(config, current with { CheckedAtMs = timestamp.ToUnixTimeMilliseconds() });
    }

    private OtaUpdateStatus ReadRaw(OtaAgentConfig config)
    {
        var path = GetStatusFilePath(config);
        if (!File.Exists(path))
        {
            return new OtaUpdateStatus(CurrentVersion: config.CurrentVersion);
        }

        try
        {
            var json = File.ReadAllText(path);
            var dto = JsonSerializer.Deserialize<OtaUpdateStatusDto>(json, Options);
            if (dto is null)
            {
                return new OtaUpdateStatus(CurrentVersion: config.CurrentVersion);
            }

            return MapFromDto(dto, config.CurrentVersion);
        }
        catch
        {
            return new OtaUpdateStatus(CurrentVersion: config.CurrentVersion);
        }
    }

    private OtaUpdateStatus MigrateLegacyCheckedAtIfNeeded(OtaAgentConfig config, OtaUpdateStatus status)
    {
        if (status.CheckedAtMs.HasValue)
        {
            return status;
        }

        var legacy = ReadLegacyCheckedAt(config.StateDirectory);
        if (!legacy.HasValue)
        {
            return status;
        }

        var migrated = status with { CheckedAtMs = legacy.Value.ToUnixTimeMilliseconds() };
        Write(config, migrated);
        return migrated;
    }

    private static DateTimeOffset? ReadLegacyCheckedAt(string stateDirectory)
    {
        var path = Path.Combine(stateDirectory, LegacyStateFileName);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var raw = File.ReadAllText(path).Trim();
            return long.TryParse(raw, out var epochMs)
                ? DateTimeOffset.FromUnixTimeMilliseconds(epochMs)
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static string GetStatusFilePath(OtaAgentConfig config) =>
        Path.Combine(FileSystemBackupService.ResolveKioskDataDir(config), StatusFileName);

    private static OtaUpdateStatus MapFromDto(OtaUpdateStatusDto dto, string fallbackCurrentVersion) =>
        new(
            dto.SchemaVersion ?? 1,
            string.IsNullOrWhiteSpace(dto.Phase) ? OtaUpdatePhases.Idle : dto.Phase,
            dto.CheckedAtMs,
            string.IsNullOrWhiteSpace(dto.CurrentVersion) ? fallbackCurrentVersion : dto.CurrentVersion,
            dto.RemoteVersion,
            dto.UpdateAvailable ?? false,
            dto.ErrorMessage);

    private static OtaUpdateStatusDto MapToDto(OtaUpdateStatus status) =>
        new()
        {
            SchemaVersion = status.SchemaVersion,
            Phase = status.Phase,
            CheckedAtMs = status.CheckedAtMs,
            CurrentVersion = status.CurrentVersion,
            RemoteVersion = status.RemoteVersion,
            UpdateAvailable = status.UpdateAvailable,
            ErrorMessage = status.ErrorMessage,
        };

    private sealed class OtaUpdateStatusDto
    {
        [JsonPropertyName("schema_version")]
        public int? SchemaVersion { get; set; }

        [JsonPropertyName("phase")]
        public string? Phase { get; set; }

        [JsonPropertyName("checked_at_ms")]
        public long? CheckedAtMs { get; set; }

        [JsonPropertyName("current_version")]
        public string? CurrentVersion { get; set; }

        [JsonPropertyName("remote_version")]
        public string? RemoteVersion { get; set; }

        [JsonPropertyName("update_available")]
        public bool? UpdateAvailable { get; set; }

        [JsonPropertyName("error_message")]
        public string? ErrorMessage { get; set; }
    }
}
