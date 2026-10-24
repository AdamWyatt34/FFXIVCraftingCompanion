using System.Text.Json.Serialization;

namespace FFXIV.Infrastructure.ExternalApis.Models;

/// <summary>
/// Fields from the XIVAPI "Recipe" sheet; request with: fields=ItemResult,CraftType,RecipeLevelTable,DifficultyFactor,QualityFactor,
///   DurabilityFactor,Ingredient,AmountIngredient,AmountResult,CanHq,RequiredCraftsmanship,RequiredControl
/// </summary>
public sealed class XivApiRecipeFields
{
    [JsonPropertyName("ItemResult")]
    public XivApiRowRef? ItemResult { get; init; }

    /// <summary>
    /// Reference to CraftType sheet. row_id maps directly to our CraftType enum.
    /// 0=CRP, 1=BSM, 2=ARM, 3=GSM, 4=LTW, 5=WVR, 6=ALC, 7=CUL
    /// </summary>
    [JsonPropertyName("CraftType")]
    public XivApiRowRef? CraftType { get; init; }

    /// <summary>
    /// Typed reference to RecipeLevelTable with nested difficulty/quality/durability base values.
    /// </summary>
    [JsonPropertyName("RecipeLevelTable")]
    public XivApiRowRef<XivApiRecipeLevelTableFields>? RecipeLevelTable { get; init; }

    [JsonPropertyName("DifficultyFactor")]
    public int DifficultyFactor { get; init; }

    [JsonPropertyName("QualityFactor")]
    public int QualityFactor { get; init; }

    [JsonPropertyName("DurabilityFactor")]
    public int DurabilityFactor { get; init; }

    [JsonPropertyName("AmountResult")]
    public int AmountResult { get; init; }

    [JsonPropertyName("CanHq")]
    public bool CanHq { get; init; }

    [JsonPropertyName("MaterialQualityFactor")]
    public int MaterialQualityFactor { get; init; }

    // The hard gate to start the craft; RecipeLevelTable's Suggested* values are only the in-game guideline.
    [JsonPropertyName("RequiredCraftsmanship")]
    public int RequiredCraftsmanship { get; init; }

    [JsonPropertyName("RequiredControl")]
    public int RequiredControl { get; init; }

    /// <summary>
    /// Array of 8 ingredient references. Empty slots have row_id == 0.
    /// </summary>
    [JsonPropertyName("Ingredient")]
    public List<XivApiRowRef>? Ingredient { get; init; }

    /// <summary>
    /// Array of 8 quantities, parallel to the Ingredient array.
    /// </summary>
    [JsonPropertyName("AmountIngredient")]
    public List<int>? AmountIngredient { get; init; }
}
