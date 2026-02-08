using System.Text.Json.Serialization;

namespace FFXIV.Infrastructure.ExternalApis.Models;

/// <summary>
/// Fields from the XIVAPI "GatheringPoint" sheet.
/// Links a GatheringPointBase to a TerritoryType (zone).
/// Request with: fields=GatheringPointBase,TerritoryType,PlaceName
/// </summary>
public sealed class XivApiGatheringPointFields
{
    [JsonPropertyName("GatheringPointBase")]
    public XivApiRowRef? GatheringPointBase { get; init; }

    [JsonPropertyName("TerritoryType")]
    public XivApiRowRef? TerritoryType { get; init; }

    [JsonPropertyName("PlaceName")]
    public XivApiRowRef? PlaceName { get; init; }
}
