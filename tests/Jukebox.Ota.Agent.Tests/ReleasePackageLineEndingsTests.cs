using Jukebox.Ota.Agent.Infrastructure.Release;

namespace Jukebox.Ota.Agent.Tests;

public class ReleasePackageLineEndingsTests
{
    [Fact]
    public void NormalizeFileToLf_RemoveCrDePipefailInvalido()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ota-crlf-{Guid.NewGuid():N}.sh");
        try
        {
            File.WriteAllText(path, "#!/bin/bash\r\nset -euo pipefail\r\n");

            ReleasePackageLineEndings.NormalizeFileToLf(path);

            var text = File.ReadAllText(path);
            Assert.DoesNotContain("\r", text);
            Assert.Contains("set -euo pipefail", text);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void NormalizeUnixScripts_NormalizaDisplaySh()
    {
        var root = Path.Combine(Path.GetTempPath(), $"ota-release-{Guid.NewGuid():N}");
        var display = Path.Combine(root, "display");
        Directory.CreateDirectory(display);
        var postInstall = Path.Combine(display, "post_install.sh");
        File.WriteAllText(postInstall, "#!/bin/bash\r\n");
        var acl = Path.Combine(display, "apply_kiosk_ota_logs_acl.sh");
        File.WriteAllText(acl, "set -e\r\n");

        try
        {
            ReleasePackageLineEndings.NormalizeUnixScripts(root, postInstall);

            Assert.DoesNotContain("\r", File.ReadAllText(postInstall));
            Assert.DoesNotContain("\r", File.ReadAllText(acl));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
