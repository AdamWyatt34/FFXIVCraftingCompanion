using System.Text.Json.Serialization;

namespace FFXIV.Infrastructure.ExternalApis.Models;

/// <summary>
/// Fields from the XIVAPI "GilShop" sheet.
/// Note: GilShop references GilShopItem (a sub-row sheet) but XIVAPI v2 does not
/// expand sub-row references as fields. Shop-to-item mapping is done via matching
/// GilShopItem.row_id == GilShop.row_id.
/// Request with: fields=Name
/// </summary>
public sealed class XivApiGilShopFields
{
    [JsonPropertyName("Name")]
    public string Name { get; init; } = string.Empty;
}
