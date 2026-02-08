using System.Text.Json.Serialization;

namespace FFXIV.Infrastructure.ExternalApis.Models;

/// <summary>
/// Fields from the XIVAPI "GatheringItem" sheet.
/// Maps a gathering item to its base Item row_id.
/// Request with: fields=Item,GatheringItemLevel,IsHidden
/// </summary>
public sealed class XivApiGatheringItemFields
{
    [JsonPropertyName("Item")]
    public XivApiRowRef? Item { get; init; }

    [JsonPropertyName("GatheringItemLevel")]
    public XivApiRowRef? GatheringItemLevel { get; init; }

    [JsonPropertyName("IsHidden")]
    public bool IsHidden { get; init; }
}
