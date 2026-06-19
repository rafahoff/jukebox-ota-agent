using Jukebox.Ota.Agent.Domain.Services;

namespace Jukebox.Ota.Agent.Infrastructure.Policy;

/// <summary>Persiste last_check_at_ms no diretório de estado do agente.</summary>
public sealed class FileOtaCheckStateStore : IOtaCheckStateStore
{
    private const string StateFileName = "last_check_at_ms";

    public DateTimeOffset? GetLastCheckAt(string stateDirectory)
    {
        var path = GetStateFilePath(stateDirectory);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var raw = File.ReadAllText(path).Trim();
            if (!long.TryParse(raw, out var epochMs))
            {
                return null;
            }

            return DateTimeOffset.FromUnixTimeMilliseconds(epochMs);
        }
        catch
        {
            return null;
        }
    }

    public void SetLastCheckAt(string stateDirectory, DateTimeOffset timestamp)
    {
        Directory.CreateDirectory(stateDirectory);
        var path = GetStateFilePath(stateDirectory);
        File.WriteAllText(path, timestamp.ToUnixTimeMilliseconds().ToString());
    }

    private static string GetStateFilePath(string stateDirectory) =>
        Path.Combine(stateDirectory, StateFileName);
}
