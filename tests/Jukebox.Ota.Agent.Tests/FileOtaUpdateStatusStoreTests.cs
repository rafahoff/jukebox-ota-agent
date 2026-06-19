using Jukebox.Ota.Agent.Domain.ValueObjects;
using Jukebox.Ota.Agent.Infrastructure.Backup;
using Jukebox.Ota.Agent.Infrastructure.Policy;

namespace Jukebox.Ota.Agent.Tests;

public class FileOtaUpdateStatusStoreTests
{
    [Fact]
    public void Write_PersisteJsonComCamposEsperados()
    {
        var root = Path.Combine(Path.GetTempPath(), $"ota-status-{Guid.NewGuid():N}");
        var config = CreateConfig(root);

        try
        {
            var store = new FileOtaUpdateStatusStore();
            store.Write(config, new OtaUpdateStatus(
                Phase: OtaUpdatePhases.UpdateAvailable,
                CheckedAtMs: 1_700_000_000_000,
                CurrentVersion: "1.0.0",
                RemoteVersion: "1.1.0",
                UpdateAvailable: true));

            var path = Path.Combine(root, "kiosk-data", "ota_update_status.json");
            Assert.True(File.Exists(path));

            var json = File.ReadAllText(path);
            Assert.Contains("\"schema_version\": 1", json);
            Assert.Contains("\"phase\": \"update_available\"", json);
            Assert.Contains("\"checked_at_ms\": 1700000000000", json);
            Assert.Contains("\"update_available\": true", json);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Read_MigraLastCheckAtMsLegado()
    {
        var root = Path.Combine(Path.GetTempPath(), $"ota-status-{Guid.NewGuid():N}");
        var stateDir = Path.Combine(root, "state");
        var config = CreateConfig(root) with { StateDirectory = stateDir };
        var legacyMs = DateTimeOffset.UtcNow.AddHours(-2).ToUnixTimeMilliseconds();

        try
        {
            Directory.CreateDirectory(stateDir);
            File.WriteAllText(Path.Combine(stateDir, "last_check_at_ms"), legacyMs.ToString());

            var store = new FileOtaUpdateStatusStore();
            var checkedAt = store.GetCheckedAt(config);

            Assert.NotNull(checkedAt);
            Assert.Equal(legacyMs, checkedAt.Value.ToUnixTimeMilliseconds());

            var statusPath = Path.Combine(root, "kiosk-data", "ota_update_status.json");
            Assert.True(File.Exists(statusPath));
            Assert.Contains($"\"checked_at_ms\": {legacyMs}", File.ReadAllText(statusPath));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Write_UsaRenameAtomico()
    {
        var root = Path.Combine(Path.GetTempPath(), $"ota-status-{Guid.NewGuid():N}");
        var config = CreateConfig(root);

        try
        {
            var store = new FileOtaUpdateStatusStore();
            store.Write(config, new OtaUpdateStatus(CurrentVersion: "1.0.0"));
            store.Write(config, new OtaUpdateStatus(
                Phase: OtaUpdatePhases.Checking,
                CurrentVersion: "1.0.0"));

            var status = store.Read(config);
            Assert.Equal(OtaUpdatePhases.Checking, status.Phase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static Domain.ValueObjects.OtaAgentConfig CreateConfig(string root)
    {
        var kioskData = Path.Combine(root, "kiosk-data");
        Directory.CreateDirectory(kioskData);

        return new Domain.ValueObjects.OtaAgentConfig(
            "machine-test",
            "beta",
            "file:///tmp/manifest.json",
            "1.0.0",
            string.Empty,
            KioskDataDir: kioskData,
            StateDirectory: Path.Combine(root, "state"));
    }
}
