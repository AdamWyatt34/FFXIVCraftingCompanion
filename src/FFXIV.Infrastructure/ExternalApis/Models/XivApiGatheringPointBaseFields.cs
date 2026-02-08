using System.Text.Json.Serialization;

namespace FFXIV.Infrastructure.ExternalApis.Models;

/// <summary>
/// Fields from the XIVAPI "GatheringPointBase" sheet.
/// Uses @as(raw) to return plain integers instead of expanded reference objects,
/// keeping the response payload small enough for large page sizes.
/// Request with: fields=GatheringType@as(raw),GatheringLevel,Item@as(raw)
/// </summary>
public sealed class XivApiGatheringPointBaseFields
{
    [JsonPropertyName("GatheringType@as(raw)")]
    public int GatheringTypeRowId { get; init; }

    [JsonPropertyName("GatheringLevel")]
    public int GatheringLevel { get; init; }

    /// <summary>
    /// Array of GatheringItem row IDs gathered at this point (raw integers).
    /// </summary>
    [JsonPropertyName("Item@as(raw)")]
    public List<int>? Item { get; init; }
}
