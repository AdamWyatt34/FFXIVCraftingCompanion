using System.Text.Json.Serialization;

namespace FFXIV.Infrastructure.ExternalApis.Models;

/// <summary>
/// Fields from the XIVAPI "Item" sheet.
/// Request with: fields=Name,Icon,LevelItem,CanBeHq,ItemSearchCategory,ItemUICategory
/// </summary>
public sealed class XivApiItemFields
{
    [JsonPropertyName("Name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("CanBeHq")]
    public bool CanBeHq { get; init; }

    [JsonPropertyName("Icon")]
    public XivApiIcon? Icon { get; init; }

    /// <summary>
    /// Reference to LevelItem sheet. The row_id IS the item level.
    /// </summary>
    [JsonPropertyName("LevelItem")]
    public XivApiRowRef? LevelItem { get; init; }

    [JsonPropertyName("ItemSearchCategory")]
    public XivApiRowRef? ItemSearchCategory { get; init; }

    [JsonPropertyName("ItemUICategory")]
    public XivApiRowRef? ItemUICategory { get; init; }
}
