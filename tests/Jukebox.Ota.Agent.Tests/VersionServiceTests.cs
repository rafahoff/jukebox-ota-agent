using Jukebox.Ota.Agent.Application.Services;

namespace Jukebox.Ota.Agent.Tests;

public class VersionServiceTests
{
    [Fact]
    public void GetVersion_RetornaFormatoSemantico()
    {
        var service = new VersionService();
        var version = service.GetVersion();

        Assert.Matches(@"^\d+\.\d+\.\d+$", version);
    }
}
