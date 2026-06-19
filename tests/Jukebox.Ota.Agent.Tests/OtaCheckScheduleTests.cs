using Jukebox.Ota.Agent.Infrastructure.Policy;

namespace Jukebox.Ota.Agent.Tests;

public class OtaCheckScheduleTests
{
    [Theory]
    [InlineData("10:00", "08:00", "18:00", true)]
    [InlineData("07:00", "08:00", "18:00", false)]
    [InlineData("19:00", "08:00", "18:00", false)]
    [InlineData("08:00", "08:00", "18:00", true)]
    [InlineData("18:00", "08:00", "18:00", true)]
    public void IsWithinWindow_JanelaNormal_RespeitaLimites(string now, string start, string end, bool expected)
    {
        var result = OtaCheckSchedule.IsWithinWindow(
            TimeOnly.Parse(now),
            TimeOnly.Parse(start),
            TimeOnly.Parse(end));

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("23:00", "22:00", "06:00", true)]
    [InlineData("03:00", "22:00", "06:00", true)]
    [InlineData("12:00", "22:00", "06:00", false)]
    [InlineData("22:00", "22:00", "06:00", true)]
    [InlineData("06:00", "22:00", "06:00", true)]
    public void IsWithinWindow_JanelaCruzaMeiaNoite_RespeitaLimites(string now, string start, string end, bool expected)
    {
        var result = OtaCheckSchedule.IsWithinWindow(
            TimeOnly.Parse(now),
            TimeOnly.Parse(start),
            TimeOnly.Parse(end));

        Assert.Equal(expected, result);
    }
}
