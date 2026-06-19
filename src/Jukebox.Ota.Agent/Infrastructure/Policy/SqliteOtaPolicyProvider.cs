using System.Globalization;
using Microsoft.Data.Sqlite;
using Jukebox.Ota.Agent.Domain.Services;
using Jukebox.Ota.Agent.Domain.ValueObjects;
using Jukebox.Ota.Agent.Infrastructure.Backup;

namespace Jukebox.Ota.Agent.Infrastructure.Policy;

/// <summary>Lê política OTA da tabela machine_config do SQLite do kiosk (somente leitura).</summary>
public sealed class SqliteOtaPolicyProvider : IOtaPolicyProvider
{
    private const int BusyTimeoutMs = 5000;

    private static readonly string[] PolicyKeys =
    [
        "ota_check_enabled",
        "ota_check_interval_minutes",
        "ota_check_interval_hours",
        "ota_check_window_start",
        "ota_check_window_end",
    ];

    public OtaCheckPolicy GetPolicy(OtaAgentConfig config)
    {
        var dbPath = Path.Combine(FileSystemBackupService.ResolveKioskDataDir(config), "jukebox_library.db");
        if (!File.Exists(dbPath))
        {
            return OtaCheckPolicy.Default;
        }

        try
        {
            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = dbPath,
                Mode = SqliteOpenMode.ReadOnly,
                DefaultTimeout = BusyTimeoutMs / 1000,
            };

            using var connection = new SqliteConnection(builder.ConnectionString);
            connection.Open();

            using (var pragma = connection.CreateCommand())
            {
                pragma.CommandText = $"PRAGMA busy_timeout = {BusyTimeoutMs};";
                pragma.ExecuteNonQuery();
            }

            var values = ReadMachineConfig(connection);
            return BuildPolicy(values);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Aviso: falha ao ler política OTA do SQLite — usando defaults ({ex.Message}).");
            return OtaCheckPolicy.Default;
        }
    }

    private static Dictionary<string, string> ReadMachineConfig(SqliteConnection connection)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        using var command = connection.CreateCommand();

        var placeholders = string.Join(", ", PolicyKeys.Select((_, i) => $"$k{i}"));
        command.CommandText = $"SELECT key, value FROM machine_config WHERE key IN ({placeholders})";

        for (var i = 0; i < PolicyKeys.Length; i++)
        {
            command.Parameters.AddWithValue($"$k{i}", PolicyKeys[i]);
        }

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var key = reader.GetString(0);
            var value = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
            result[key] = value;
        }

        return result;
    }

    public static OtaCheckPolicy BuildPolicy(IReadOnlyDictionary<string, string> values)
    {
        var defaults = OtaCheckPolicy.Default;

        var enabled = ParseEnabled(values, defaults.Enabled);
        var intervalMinutes = ParseIntervalMinutes(values, defaults.IntervalMinutes);
        var windowStart = ParseTime(values, "ota_check_window_start", defaults.WindowStart);
        var windowEnd = ParseTime(values, "ota_check_window_end", defaults.WindowEnd);

        return new OtaCheckPolicy(enabled, intervalMinutes, windowStart, windowEnd);
    }

    private static bool ParseEnabled(IReadOnlyDictionary<string, string> values, bool defaultValue)
    {
        if (!values.TryGetValue("ota_check_enabled", out var raw) || string.IsNullOrWhiteSpace(raw))
        {
            return defaultValue;
        }

        if (bool.TryParse(raw, out var parsed))
        {
            return parsed;
        }

        return raw.Trim() switch
        {
            "1" or "yes" or "sim" => true,
            "0" or "no" or "nao" or "não" => false,
            _ => defaultValue,
        };
    }

    internal static int ParseIntervalMinutes(IReadOnlyDictionary<string, string> values, int defaultValue)
    {
        if (values.TryGetValue("ota_check_interval_minutes", out var minutesRaw) &&
            int.TryParse(minutesRaw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var minutes))
        {
            return OtaCheckPolicy.SnapIntervalMinutes(minutes);
        }

        if (values.TryGetValue("ota_check_interval_hours", out var hoursRaw) &&
            int.TryParse(hoursRaw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var hours))
        {
            return OtaCheckPolicy.MigrateFromHours(hours);
        }

        return defaultValue;
    }

    private static TimeOnly ParseTime(IReadOnlyDictionary<string, string> values, string key, TimeOnly defaultValue)
    {
        if (!values.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
        {
            return defaultValue;
        }

        return TimeOnly.TryParseExact(raw.Trim(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : defaultValue;
    }
}
