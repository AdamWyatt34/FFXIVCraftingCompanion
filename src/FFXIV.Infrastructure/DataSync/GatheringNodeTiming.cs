using FFXIV.Domain.Enums;
using FFXIV.Domain.Utilities;
using FFXIV.Infrastructure.ExternalApis.Models;

namespace FFXIV.Infrastructure.DataSync;

/// <summary>
/// Turns a gathering point's GatheringPointTransient row into what the sync stores: whether the
/// node is timed, its spawn windows in the <see cref="SpawnWindows"/> format, and its node type.
/// </summary>
public static class GatheringNodeTiming
{
    public readonly record struct Result(bool IsTimed, string? SpawnTimes, NodeType NodeType)
    {
        public static readonly Result Untimed = new(false, null, NodeType.Standard);
    }

    public static Result Resolve(XivApiGatheringPointTransientFields row) =>
        Resolve(
            row.EphemeralStartTime,
            row.EphemeralEndTime,
            row.GatheringRarePopTimeTable?.Fields?.StartTime ?? [],
            row.GatheringRarePopTimeTable?.Fields?.Duration ?? []);

    /// <param name="ephemeralStartTime">HHMM, or 65535 / 0 for none.</param>
    /// <param name="ephemeralEndTime">HHMM, or 65535 / 0 for none.</param>
    /// <param name="rarePopStartTimes">Up to three HHMM starts; 65535 for an unused slot.</param>
    /// <param name="rarePopDurations">Matching lengths as hours × 100 + minutes; 0 for an unused slot.</param>
    public static Result Resolve(
        int ephemeralStartTime,
        int ephemeralEndTime,
        IReadOnlyList<int> rarePopStartTimes,
        IReadOnlyList<int> rarePopDurations)
    {
        // Ephemeral nodes carry one window as a start and an end. Rows with no data hold either
        // the sentinel in both fields or zero in both; neither is a window.
        if (ephemeralStartTime != 0 || ephemeralEndTime != 0)
        {
            var start = SpawnWindows.HourFromGameTime(ephemeralStartTime);
            var end = SpawnWindows.HourFromGameTime(ephemeralEndTime);
            if (start is { } s && end is { } e && s != e)
                return new Result(true, SpawnWindows.Serialize([new SpawnWindow(s, e)]), NodeType.Ephemeral);
        }

        // Unspoiled and legendary nodes carry up to three windows as a start and a length.
        var windows = new List<SpawnWindow>();
        for (var i = 0; i < rarePopStartTimes.Count && i < rarePopDurations.Count; i++)
        {
            var start = SpawnWindows.HourFromGameTime(rarePopStartTimes[i]);
            var hours = RarePopDurationHours(rarePopDurations[i]);
            if (start is { } s && hours is { } h)
                windows.Add(new SpawnWindow(s, (s + h) % 24));
        }

        return windows.Count > 0
            ? new Result(true, SpawnWindows.Serialize(windows), NodeType.Unspoiled)
            : Result.Untimed;
    }

    /// <summary>
    /// A rare-pop duration is written as hours × 100 + minutes, with the minutes allowed to run to
    /// 60: the A Realm Reborn tables hold 300 (three hours) and every later table 160 (1:60, two
    /// hours), which is what the nodes last in game. Whole hours only; anything shorter is dropped.
    /// </summary>
    internal static int? RarePopDurationHours(int encoded)
    {
        if (encoded <= 0 || encoded == SpawnWindows.NoTime)
            return null;

        var minutes = encoded / 100 * 60 + encoded % 100;
        var hours = (int)Math.Round(minutes / 60.0, MidpointRounding.AwayFromZero);
        return hours is >= 1 and <= 23 ? hours : null;
    }
}
