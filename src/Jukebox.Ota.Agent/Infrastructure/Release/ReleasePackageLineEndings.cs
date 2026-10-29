namespace Jukebox.Ota.Agent.Infrastructure.Release;

/// <summary>Normaliza CRLF em scripts Linux extraídos de pacotes OTA (empacote Windows).</summary>
internal static class ReleasePackageLineEndings
{
    internal static void NormalizeUnixScripts(string releaseDirectory, string postInstallScriptPath)
    {
        var paths = new HashSet<string>(StringComparer.Ordinal);
        if (File.Exists(postInstallScriptPath))
        {
            paths.Add(Path.GetFullPath(postInstallScriptPath));
        }

        var displayDir = Path.Combine(releaseDirectory, "display");
        if (Directory.Exists(displayDir))
        {
            foreach (var sh in Directory.EnumerateFiles(displayDir, "*.sh", SearchOption.TopDirectoryOnly))
            {
                paths.Add(Path.GetFullPath(sh));
            }
        }

        foreach (var path in paths)
        {
            NormalizeFileToLf(path);
        }
    }

    internal static void NormalizeFileToLf(string path)
    {
        var text = File.ReadAllText(path);
        if (!text.Contains('\r', StringComparison.Ordinal))
        {
            return;
        }

        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal);
        File.WriteAllText(path, normalized);
    }
}
