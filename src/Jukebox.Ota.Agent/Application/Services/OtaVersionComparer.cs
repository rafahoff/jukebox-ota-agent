namespace Jukebox.Ota.Agent.Application.Services;

/// <summary>Compara versões semver OTA (major.minor.patch), ignorando sufixo após o primeiro hífen.</summary>
public static class OtaVersionComparer
{
    /// <summary>Retorna true apenas se <paramref name="remote"/> for estritamente mais recente que <paramref name="current"/>.</summary>
    public static bool IsNewer(string remote, string current)
    {
        var remoteParts = ParseVersion(Normalize(remote));
        var currentParts = ParseVersion(Normalize(current));

        if (remoteParts.Major != currentParts.Major)
        {
            return remoteParts.Major > currentParts.Major;
        }

        if (remoteParts.Minor != currentParts.Minor)
        {
            return remoteParts.Minor > currentParts.Minor;
        }

        return remoteParts.Patch > currentParts.Patch;
    }

    /// <summary>Remove sufixo de pré-release/local (ex.: 1.0.34-local → 1.0.34).</summary>
    public static string Normalize(string version)
    {
        var trimmed = version.Trim();
        var dashIndex = trimmed.IndexOf('-');
        return dashIndex >= 0 ? trimmed[..dashIndex] : trimmed;
    }

    private static (int Major, int Minor, int Patch) ParseVersion(string version)
    {
        var parts = version.Split('.');
        return (ParsePart(parts, 0), ParsePart(parts, 1), ParsePart(parts, 2));
    }

    private static int ParsePart(string[] parts, int index) =>
        index < parts.Length && int.TryParse(parts[index], out var value) ? value : 0;
}
