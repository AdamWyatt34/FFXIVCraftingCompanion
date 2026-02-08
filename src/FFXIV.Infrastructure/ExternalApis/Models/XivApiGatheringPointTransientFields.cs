using System.Text.Json.Serialization;

namespace FFXIV.Infrastructure.ExternalApis.Models;

/// <summary>
/// Fields from the XIVAPI "GatheringPointTransient" sheet.
/// Indicates timed node spawn/despawn Eorzea times.
/// row_id matches GatheringPoint row_id.
/// Request with: fields=EphemeralStartTime,EphemeralEndTime
/// </summary>
public sealed class XivApiGatheringPointTransientFields
{
    [JsonPropertyName("EphemeralStartTime")]
    public int EphemeralStartTime { get; init; }

    [JsonPropertyName("EphemeralEndTime")]
    public int EphemeralEndTime { get; init; }
}
