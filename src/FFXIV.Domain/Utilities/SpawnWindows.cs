using System.Text.Json;

namespace FFXIV.Domain.Utilities;

/// <summary>An Eorzean-time spawn window in whole hours. Evaluated by <see cref="EorzeanTime"/>.</summary>
public readonly record struct SpawnWindow(int StartHour, int EndHour);

/// <summary>
/// The one reader and writer of <c>GatheringInfo.SpawnTimes</c>. Storage is a JSON array of
/// <c>"H:MM"</c> strings in start/end pairs: <c>["20:00","0:00"]</c> is one window,
/// <c>["2:00","4:00","10:00","12:00"]</c> two. The parser also accepts what the sync wrote before
/// this format existed: raw game-data HHMM values (<c>"2000:00"</c> is 20:00) and the 65535
/// "no time" sentinel, which yields no window, so published data from before the fix still renders.
/// </summary>
public static class SpawnWindows
{
    /// <summary>The game data's "no value" for ephemeral and rare-pop times.</summary>
    public const int NoTime = 65535;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    /// <summary>Canonical storage form, or null when there are no windows.</summary>
    public static string? Serialize(IReadOnlyList<SpawnWindow> windows)
    {
        if (windows.Count == 0)
            return null;

        var parts = new string[windows.Count * 2];
        for (var i = 0; i < windows.Count; i++)
        {
            parts[i * 2] = $"{windows[i].StartHour}:00";
            parts[i * 2 + 1] = $"{windows[i].EndHour}:00";
        }

        return JsonSerializer.Serialize(parts, JsonOptions);
    }

    /// <summary>
    /// Parses stored spawn times. Never throws: anything unreadable, a sentinel pair, an hour
    /// outside 0–23 or an empty window (start == end) is dropped. Order is preserved.
    /// </summary>
    public static IReadOnlyList<SpawnWindow> Parse(string? spawnTimes)
    {
        if (string.IsNullOrWhiteSpace(spawnTimes))
            return [];

        JsonElement root;
        try
        {
            using var document = JsonDocument.Parse(spawnTimes);
            root = document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return [];
        }

        if (root.ValueKind != JsonValueKind.Array)
            return [];

        var hours = new List<int?>();
        foreach (var element in root.EnumerateArray())
            hours.Add(HourFromElement(element));

        var windows = new List<SpawnWindow>();
        for (var i = 0; i + 1 < hours.Count; i += 2)
        {
            if (hours[i] is { } start && hours[i + 1] is { } end && start != end)
                windows.Add(new SpawnWindow(start, end));
        }

        return windows;
    }

    /// <summary>
    /// The hour of a game-data time value: either a plain hour (0–23) or an HHMM value
    /// (<c>2000</c> is 20:00). Null for the <see cref="NoTime"/> sentinel and anything out of range.
    /// Minutes are dropped: windows are evaluated on whole Eorzean hours.
    /// </summary>
    public static int? HourFromGameTime(int value)
    {
        if (value < 0 || value == NoTime)
            return null;

        if (value <= 23)
            return value;

        if (value < 100)
            return null;

        var hour = value / 100;
        var minute = value % 100;
        return hour <= 23 && minute <= 59 ? hour : null;
    }

    private static int? HourFromElement(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Number:
                return element.TryGetInt32(out var number) ? HourFromGameTime(number) : null;

            case JsonValueKind.String:
            {
                var text = element.GetString()!;
                var colon = text.IndexOf(':');
                var hourText = (colon < 0 ? text : text[..colon]).Trim();
                var minuteText = colon < 0 ? "0" : text[(colon + 1)..].Trim();
                if (!IsDigits(hourText) || !IsDigits(minuteText))
                    return null;
                if (!int.TryParse(hourText, out var value) || !int.TryParse(minuteText, out var minute) || minute > 59)
                    return null;
                return HourFromGameTime(value);
            }

            default:
                return null;
        }
    }

    private static bool IsDigits(string text)
    {
        if (text.Length == 0)
            return false;
        foreach (var c in text)
        {
            if (c is < '0' or > '9')
                return false;
        }
        return true;
    }
}
