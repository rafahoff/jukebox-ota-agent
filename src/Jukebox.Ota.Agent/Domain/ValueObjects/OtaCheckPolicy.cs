namespace Jukebox.Ota.Agent.Domain.ValueObjects;

/// <summary>Política de verificação OTA lida do kiosk (machine_config) ou defaults.</summary>
public sealed record OtaCheckPolicy(
    bool Enabled,
    int IntervalMinutes,
    TimeOnly WindowStart,
    TimeOnly WindowEnd)
{
    public const int IntervalMinutesMin = 5;
    public const int IntervalMinutesMax = 60;
    public const int IntervalMinutesStep = 5;
    public const int DefaultIntervalMinutes = 30;

    public static OtaCheckPolicy Default { get; } = new(
        Enabled: true,
        IntervalMinutes: DefaultIntervalMinutes,
        WindowStart: new TimeOnly(0, 0),
        WindowEnd: new TimeOnly(23, 59));

    public static int SnapIntervalMinutes(int minutes)
    {
        var clamped = Math.Clamp(minutes, IntervalMinutesMin, IntervalMinutesMax);
        var snapped = (int)(Math.Round(clamped / (double)IntervalMinutesStep) * IntervalMinutesStep);
        return Math.Clamp(snapped, IntervalMinutesMin, IntervalMinutesMax);
    }

    /// <summary>Migra legado em horas para minutos (máx. 60).</summary>
    public static int MigrateFromHours(int hours)
    {
        var clampedHours = Math.Clamp(hours, 1, 168);
        return SnapIntervalMinutes(Math.Min(clampedHours * 60, IntervalMinutesMax));
    }
}
