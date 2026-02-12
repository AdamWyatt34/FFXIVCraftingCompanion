using System.Text.Json.Serialization;

namespace FFXIV.Infrastructure.ExternalApis.Models;

/// <summary>
/// Fields from the RecipeLevelTable sheet. These are base values
/// that get multiplied by the recipe's factor fields (e.g., DifficultyFactor / 100).
/// </summary>
public sealed class XivApiRecipeLevelTableFields
{
    [JsonPropertyName("ClassJobLevel")]
    public int ClassJobLevel { get; init; }

    [JsonPropertyName("Stars")]
    public int Stars { get; init; }

    [JsonPropertyName("Difficulty")]
    public int Difficulty { get; init; }

    [JsonPropertyName("Quality")]
    public int Quality { get; init; }

    [JsonPropertyName("Durability")]
    public int Durability { get; init; }

    [JsonPropertyName("SuggestedCraftsmanship")]
    public int SuggestedCraftsmanship { get; init; }

    [JsonPropertyName("SuggestedControl")]
    public int SuggestedControl { get; init; }

    [JsonPropertyName("ProgressDivider")]
    public int ProgressDivider { get; init; }

    [JsonPropertyName("ProgressModifier")]
    public int ProgressModifier { get; init; }

    [JsonPropertyName("QualityDivider")]
    public int QualityDivider { get; init; }

    [JsonPropertyName("QualityModifier")]
    public int QualityModifier { get; init; }
}
