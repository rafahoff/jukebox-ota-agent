namespace Jukebox.Ota.Agent.Domain.ValueObjects;

/// <summary>Convenção de nomes de artefatos OTA por produto (<c>app</c>).</summary>
public static class OtaPackageNaming
{
    public const string DefaultApp = "jukeeo";

    public static string ResolveApp(string? app) =>
        string.IsNullOrWhiteSpace(app) ? DefaultApp : app.Trim();

    public static string BuildPackageFileName(string app, string version, string arch) =>
        $"{ResolveApp(app)}-{version}+{arch}.tar.zst";

    public static string BuildManifestCacheFileName(string app, string version) =>
        $"{ResolveApp(app)}-{version}-manifest.json";
}
