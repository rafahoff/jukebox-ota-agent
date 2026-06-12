using System.Reflection;

namespace Jukebox.Ota.Agent.Application.Services;

public sealed class VersionService
{
    public string GetVersion()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        return version is null ? "0.0.0" : $"{version.Major}.{version.Minor}.{version.Build}";
    }
}
