namespace Swarm.Server.Diagnostics;

public static class DurationFormatter
{
    public static string Format(long totalSeconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(totalSeconds);

        const int SecondsPerMinute = 60;
        const int SecondsPerHour = SecondsPerMinute * 60;

        long hours = totalSeconds / SecondsPerHour;
        long minutes = totalSeconds % SecondsPerHour / SecondsPerMinute;
        long seconds = totalSeconds % SecondsPerMinute;

        return $"{hours} h {minutes} min {seconds} s";
    }
}
