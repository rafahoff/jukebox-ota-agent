namespace Jukebox.Ota.Agent.Domain.Services;

/// <summary>Persiste o instante da última verificação OTA bem-sucedida.</summary>
public interface IOtaCheckStateStore
{
    DateTimeOffset? GetLastCheckAt(string stateDirectory);

    void SetLastCheckAt(string stateDirectory, DateTimeOffset timestamp);
}
