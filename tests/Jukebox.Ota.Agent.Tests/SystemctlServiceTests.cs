using Jukebox.Ota.Agent.Infrastructure.System;

namespace Jukebox.Ota.Agent.Tests;

public sealed class SystemctlServiceTests
{
    [Fact]
    public void BuildSystemctlInvocation_EmAmbienteNaoLinux_UsaSystemctlDirecto()
    {
        var (fileName, arguments) = SystemctlService.BuildSystemctlInvocation("stop jukeeo_kiosk_flutterpi.service");

        Assert.Equal("/bin/systemctl", fileName);
        Assert.Equal("stop jukeeo_kiosk_flutterpi.service", arguments);
    }

    [Theory]
    [InlineData("stop jukeeo_kiosk_flutterpi", "stop jukeeo_kiosk_flutterpi.service")]
    [InlineData("is-active jukeeo_kiosk_flutterpi", "is-active jukeeo_kiosk_flutterpi.service")]
    public void NormalizeSystemctlArguments_AdicionaSufixoService(string input, string expected)
    {
        var normalized = SystemctlService.NormalizeSystemctlArguments(input);
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(3, true)]
    [InlineData(4, false)]
    [InlineData(1, false)]
    public void IsServiceUnitInstalledFromIsActiveExitCode_InterpretaCodigosSystemd(int exitCode, bool expectedInstalled)
    {
        var installed = SystemctlService.IsServiceUnitInstalledFromIsActiveExitCode(exitCode);
        Assert.Equal(expectedInstalled, installed);
    }
}
