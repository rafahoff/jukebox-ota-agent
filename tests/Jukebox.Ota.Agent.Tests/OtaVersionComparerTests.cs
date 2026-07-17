using Jukebox.Ota.Agent.Application.Services;

namespace Jukebox.Ota.Agent.Tests;

public class OtaVersionComparerTests
{
    [Theory]
    [InlineData("1.0.32", "1.0.34-local", false)]
    [InlineData("1.0.32", "1.0.34", false)]
    [InlineData("1.0.35", "1.0.34", true)]
    [InlineData("1.0.34", "1.0.34", false)]
    [InlineData("1.0.34", "1.0.34-local", false)]
    [InlineData("2.0.0", "1.9.9", true)]
    [InlineData("1.1.0", "1.0.99", true)]
    public void IsNewer_ComparaSemverNumerico(string remote, string current, bool expected)
    {
        Assert.Equal(expected, OtaVersionComparer.IsNewer(remote, current));
    }

    [Theory]
    [InlineData("1.0.34-local", "1.0.34")]
    [InlineData("1.0.0", "1.0.0")]
    [InlineData("2.1.3-beta", "2.1.3")]
    public void Normalize_RemoveSufixoAposHifen(string input, string expected)
    {
        Assert.Equal(expected, OtaVersionComparer.Normalize(input));
    }
}
