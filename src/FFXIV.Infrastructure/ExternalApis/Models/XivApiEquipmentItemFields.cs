using System.Text.Json.Serialization;

namespace FFXIV.Infrastructure.ExternalApis.Models;

/// <summary>
/// Fields from the XIVAPI "Item" sheet for equipment sync.
/// Uses @as(raw) for reference fields to return plain integers.
/// Request with: fields=Name,Icon,CanBeHq,LevelItem@as(raw),Rarity,EquipSlotCategory@as(raw),ClassJobCategory@as(raw),LevelEquip,BaseParam@as(raw),BaseParamValue,MateriaSlotCount
/// </summary>
public sealed class XivApiEquipmentItemFields
{
    [JsonPropertyName("Name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("Icon")]
    public XivApiIcon? Icon { get; init; }

    [JsonPropertyName("CanBeHq")]
    public bool CanBeHq { get; init; }

    [JsonPropertyName("Rarity")]
    public int Rarity { get; init; }

    /// <summary>
    /// With @as(raw), LevelItem returns the row_id directly as int (row_id IS item level).
    /// </summary>
    [JsonPropertyName("LevelItem@as(raw)")]
    public int LevelItem { get; init; }

    [JsonPropertyName("LevelEquip")]
    public int LevelEquip { get; init; }

    [JsonPropertyName("EquipSlotCategory@as(raw)")]
    public int EquipSlotCategoryRowId { get; init; }

    [JsonPropertyName("ClassJobCategory@as(raw)")]
    public int ClassJobCategoryRowId { get; init; }

    /// <summary>
    /// Array of BaseParam row_ids (parallel with BaseParamValue).
    /// </summary>
    [JsonPropertyName("BaseParam@as(raw)")]
    public List<int>? BaseParam { get; init; }

    /// <summary>
    /// Array of stat values (parallel with BaseParam).
    /// </summary>
    [JsonPropertyName("BaseParamValue")]
    public List<int>? BaseParamValue { get; init; }

    [JsonPropertyName("MateriaSlotCount")]
    public int MateriaSlotCount { get; init; }
}
