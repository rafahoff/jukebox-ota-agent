using Microsoft.Data.Sqlite;
using Jukebox.Ota.Agent.Domain.ValueObjects;
using Jukebox.Ota.Agent.Infrastructure.Policy;

namespace Jukebox.Ota.Agent.Tests;

public class SqliteOtaPolicyProviderTests
{
    [Fact]
    public void GetPolicy_DbInexistente_RetornaDefaults()
    {
        var config = new OtaAgentConfig(
            "machine-001",
            "stable",
            "file:///tmp/manifest.json",
            "1.0.0",
            string.Empty,
            KioskDataDir: Path.Combine(Path.GetTempPath(), $"ota-missing-{Guid.NewGuid():N}"));

        var provider = new SqliteOtaPolicyProvider();
        var policy = provider.GetPolicy(config);

        Assert.Equal(OtaCheckPolicy.Default, policy);
    }

    [Fact]
    public void GetPolicy_LeChavesMachineConfigEmMinutos()
    {
        var root = Path.Combine(Path.GetTempPath(), $"ota-policy-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var dbPath = Path.Combine(root, "jukebox_library.db");

        try
        {
            CreateMachineConfigDb(dbPath, new Dictionary<string, string>
            {
                ["ota_check_enabled"] = "false",
                ["ota_check_interval_minutes"] = "25",
                ["ota_check_window_start"] = "02:00",
                ["ota_check_window_end"] = "05:00",
            });

            var config = new OtaAgentConfig(
                "machine-001",
                "stable",
                "file:///tmp/manifest.json",
                "1.0.0",
                string.Empty,
                KioskDataDir: root);

            var policy = new SqliteOtaPolicyProvider().GetPolicy(config);

            Assert.False(policy.Enabled);
            Assert.Equal(25, policy.IntervalMinutes);
            Assert.Equal(new TimeOnly(2, 0), policy.WindowStart);
            Assert.Equal(new TimeOnly(5, 0), policy.WindowEnd);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void BuildPolicy_LegadoEmHoras_MigraParaMinutos()
    {
        var policy = SqliteOtaPolicyProvider.BuildPolicy(new Dictionary<string, string>
        {
            ["ota_check_interval_hours"] = "12",
        });

        Assert.Equal(60, policy.IntervalMinutes);
    }

    [Fact]
    public void BuildPolicy_MinutosForaDoLimite_FazSnapEClamp()
    {
        var policy = SqliteOtaPolicyProvider.BuildPolicy(new Dictionary<string, string>
        {
            ["ota_check_interval_minutes"] = "47",
        });

        Assert.Equal(45, policy.IntervalMinutes);
    }

    [Fact]
    public void BuildPolicy_ChaveAusente_MantemDefault()
    {
        var policy = SqliteOtaPolicyProvider.BuildPolicy(new Dictionary<string, string>());

        Assert.True(policy.Enabled);
        Assert.Equal(30, policy.IntervalMinutes);
        Assert.Equal(new TimeOnly(0, 0), policy.WindowStart);
        Assert.Equal(new TimeOnly(23, 59), policy.WindowEnd);
    }

    private static void CreateMachineConfigDb(string dbPath, IReadOnlyDictionary<string, string> rows)
    {
        using var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();

        using (var create = connection.CreateCommand())
        {
            create.CommandText = """
                CREATE TABLE machine_config (
                  key TEXT PRIMARY KEY,
                  value TEXT
                );
                """;
            create.ExecuteNonQuery();
        }

        foreach (var (key, value) in rows)
        {
            using var insert = connection.CreateCommand();
            insert.CommandText = "INSERT INTO machine_config (key, value) VALUES ($key, $value);";
            insert.Parameters.AddWithValue("$key", key);
            insert.Parameters.AddWithValue("$value", value);
            insert.ExecuteNonQuery();
        }
    }
}
