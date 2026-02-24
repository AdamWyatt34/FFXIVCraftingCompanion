namespace FFXIV.Domain.Utilities;

public static class EorzeanTime
{
    public const double EorzeanHourInSeconds = 175.0;
    public const double EorzeanDayInSeconds = 4200.0;

    public static int GetCurrentEorzeanHour()
    {
        return GetEorzeanHour(DateTimeOffset.UtcNow);
    }

    public static int GetEorzeanHour(DateTimeOffset utcTime)
    {
        var epochSeconds = utcTime.ToUnixTimeSeconds();
        var eorzeanTotalHours = epochSeconds * (3600.0 / EorzeanHourInSeconds) / 3600.0;
        return (int)(eorzeanTotalHours % 24);
    }

    public static bool IsWindowActive(int startHour, int endHour)
    {
        return IsWindowActive(startHour, endHour, GetCurrentEorzeanHour());
    }

    public static bool IsWindowActive(int startHour, int endHour, int currentHour)
    {
        if (startHour == endHour)
            return false;

        if (startHour < endHour)
            return currentHour >= startHour && currentHour < endHour;

        // Midnight wrap (e.g., 22:00-02:00)
        return currentHour >= startHour || currentHour < endHour;
    }

    public static TimeSpan TimeUntilWindow(int startHour, int endHour)
    {
        return TimeUntilWindow(startHour, endHour, GetCurrentEorzeanHour());
    }

    public static TimeSpan TimeUntilWindow(int startHour, int endHour, int currentHour)
    {
        if (IsWindowActive(startHour, endHour, currentHour))
            return TimeSpan.Zero;

        var hoursUntil = (startHour - currentHour + 24) % 24;
        return TimeSpan.FromSeconds(hoursUntil * EorzeanHourInSeconds);
    }
}
