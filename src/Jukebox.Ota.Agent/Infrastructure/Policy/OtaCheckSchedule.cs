namespace Jukebox.Ota.Agent.Infrastructure.Policy;

/// <summary>Regras de janela horária para check/apply OTA (hora local do dispositivo).</summary>
public static class OtaCheckSchedule
{
    /// <summary>
    /// Indica se <paramref name="now"/> está dentro da janela [<paramref name="windowStart"/>, <paramref name="windowEnd"/>].
    /// Suporta janela que cruza a meia-noite (ex.: 22:00–06:00).
    /// </summary>
    public static bool IsWithinWindow(TimeOnly now, TimeOnly windowStart, TimeOnly windowEnd)
    {
        if (windowStart <= windowEnd)
        {
            return now >= windowStart && now <= windowEnd;
        }

        return now >= windowStart || now <= windowEnd;
    }
}
