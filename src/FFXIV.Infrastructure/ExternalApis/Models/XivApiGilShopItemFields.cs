using System.Text.Json.Serialization;

namespace FFXIV.Infrastructure.ExternalApis.Models;

/// <summary>
/// Fields from the XIVAPI "GilShopItem" sub-row sheet.
/// Uses @as(raw) to return the Item as a plain integer (row_id) for smaller payloads.
/// Request with: fields=Item@as(raw)
/// </summary>
public sealed class XivApiGilShopItemFields
{
    [JsonPropertyName("Item@as(raw)")]
    public int ItemRowId { get; init; }
}
