using System.Text.Json.Serialization;

namespace FFXIV.Infrastructure.ExternalApis.Models;

/// <summary>
/// Request with: fields=ItemAction.Data,ItemAction.DataHQ
/// </summary>
public sealed class XivApiConsumableItemFields
{
    [JsonPropertyName("ItemAction")]
    public XivApiRowRef<XivApiItemActionFields>? ItemAction { get; init; }
}

/// <summary>
/// For a consumable, Data[0] is the status it applies (48 Well Fed, 49 Medicated) and Data[1]
/// the ItemFood row holding its bonuses, NQ and HQ alike.
/// </summary>
public sealed class XivApiItemActionFields
{
    [JsonPropertyName("Data")]
    public List<int>? Data { get; init; }

    [JsonPropertyName("DataHQ")]
    public List<int>? DataHQ { get; init; }
}
