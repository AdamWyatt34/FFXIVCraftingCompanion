using System.Text.Json.Serialization;

namespace FFXIV.Infrastructure.ExternalApis.Models;

/// <summary>
/// Fields from the XIVAPI "GatheringPointTransient" sheet (one row per gathering point).
/// Request with:
/// fields=EphemeralStartTime,EphemeralEndTime,GatheringRarePopTimeTable.StartTime,GatheringRarePopTimeTable.Duration
/// </summary>
public sealed class XivApiGatheringPointTransientFields
{
    public const string RequestFields =
        "EphemeralStartTime,EphemeralEndTime,GatheringRarePopTimeTable.StartTime,GatheringRarePopTimeTable.Duration";

    /// <summary>Ephemeral window start as HHMM (2000 is 20:00); 65535 when the node is not ephemeral.</summary>
    [JsonPropertyName("EphemeralStartTime")]
    public int EphemeralStartTime { get; init; }

    /// <summary>Ephemeral window end as HHMM; 65535 when the node is not ephemeral.</summary>
    [JsonPropertyName("EphemeralEndTime")]
    public int EphemeralEndTime { get; init; }

    /// <summary>Unspoiled and legendary nodes link a time table; row 0 means none.</summary>
    [JsonPropertyName("GatheringRarePopTimeTable")]
    public XivApiRowRef<XivApiGatheringRarePopTimeTableFields>? GatheringRarePopTimeTable { get; init; }
}

/// <summary>
/// Fields from the XIVAPI "GatheringRarePopTimeTable" sheet: up to three windows per node.
/// </summary>
public sealed class XivApiGatheringRarePopTimeTableFields
{
    /// <summary>Window starts as HHMM; 65535 for an unused slot.</summary>
    [JsonPropertyName("StartTime")]
    public List<int>? StartTime { get; init; }

    /// <summary>
    /// Window lengths as hours × 100 + minutes: 300 (3:00) for the A Realm Reborn tables, 160
    /// (1:60, two hours) for every expansion since; 0 for an unused slot.
    /// </summary>
    [JsonPropertyName("Duration")]
    public List<int>? Duration { get; init; }
}
