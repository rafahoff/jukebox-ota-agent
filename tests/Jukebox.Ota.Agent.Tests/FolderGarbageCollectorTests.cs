using Jukebox.Ota.Agent.Infrastructure.Release;

namespace Jukebox.Ota.Agent.Tests;

public class FolderGarbageCollectorTests
{
    [Fact]
    public void Collect_PreservaProtegidos_RemoveMaisAntigos()
    {
        var root = Path.Combine(Path.GetTempPath(), $"ota-gc-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        var keepA = Path.Combine(root, "1.4.0+aarch64");
        var keepB = Path.Combine(root, "1.4.1+aarch64");
        var old1 = Path.Combine(root, "1.3.0+aarch64");
        var old2 = Path.Combine(root, "1.2.0+aarch64");
        var old3 = Path.Combine(root, "1.1.0+aarch64");

        foreach (var dir in new[] { keepA, keepB, old1, old2, old3 })
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "marker.txt"), "x");
        }

        try
        {
            var protectedNames = new HashSet<string>(StringComparer.Ordinal)
            {
                "1.4.0+aarch64",
                "1.4.1+aarch64",
            };

            FolderGarbageCollector.Collect(root, protectedNames, maxFolders: 4);

            Assert.True(Directory.Exists(keepA));
            Assert.True(Directory.Exists(keepB));
            Assert.True(Directory.Exists(old1));
            Assert.True(Directory.Exists(old2));
            Assert.False(Directory.Exists(old3));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
