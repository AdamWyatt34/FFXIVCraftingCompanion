using FFXIV.Application.DTOs;
using FFXIV.Application.Recipes.DTOs;

namespace FFXIV.Application.Journey.DTOs;

public sealed record StatSufficiencyResultDto(
    int RecipeId,
    bool MeetsMinimumRequirements,
    bool CraftsmanshipSufficient,
    bool ControlSufficient,
    bool LevelSufficient,
    int CraftsmanshipDeficit,
    int ControlDeficit,
    int LevelDeficit,
    int EstimatedHQPercent,
    double EstimatedQualityPercent,
    int EstimatedSteps,
    bool CanComplete,
    string Recommendation,
    List<GearUpgradeSuggestionDto> SuggestedGearUpgrades);

public sealed record GearUpgradeSuggestionDto(
    string Slot,
    string? CurrentItem,
    string SuggestedItem,
    int SuggestedEquipmentId,
    int CraftsmanshipGain,
    int ControlGain,
    int CPGain,
    string? Source,
    int? RecipeId);

public sealed record ItemContextDto(
    int ItemId,
    string ItemName,
    string IconUrl,
    List<RecipeLinkDto> Recipes,
    bool CanSimulate,
    bool CanSolve,
    List<RecipeLinkDto> UsedInRecipes,
    bool IsEquipment,
    EquipmentLinkDto? EquipmentInfo,
    List<GatheringLinkDto> GatheringInfo,
    List<VendorLinkDto> VendorSources);

public sealed record RecipeLinkDto(
    int RecipeId,
    string ResultItemName,
    string IconUrl,
    string CraftType,
    int Level,
    int Stars);

public sealed record EquipmentLinkDto(
    int EquipmentId,
    string EquipSlot,
    int Craftsmanship,
    int Control,
    int CP,
    int Gathering,
    int Perception,
    int GP);

public sealed record GatheringLinkDto(
    string Zone,
    string? Coordinates,
    string GatheringType,
    string GatheringClass,
    int GatheringLevel,
    bool IsTimed,
    string? SpawnTimes);

public sealed record VendorLinkDto(
    string NpcName,
    string Zone,
    int Cost,
    string Currency);

public sealed record CraftingJourneyDto(
    RecipeDto Recipe,
    MaterialTreeResultDto Materials,
    StatSufficiencyResultDto? Sufficiency,
    List<GearUpgradeSuggestionDto> GearUpgrades,
    ItemContextDto ItemContext);

public sealed record SufficiencyRequestDto
{
    public int? Craftsmanship { get; init; }
    public int? Control { get; init; }
    public int? CP { get; init; }
    public int? Level { get; init; }
    public bool UseProfileStats { get; init; }
}
