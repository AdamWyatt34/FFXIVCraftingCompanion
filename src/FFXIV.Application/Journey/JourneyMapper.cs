using FFXIV.Application.Journey.DTOs;
using FFXIV.Domain.Models;

namespace FFXIV.Application.Journey;

public static class JourneyMapper
{
    public static StatSufficiencyResultDto ToDto(StatSufficiencyResult result)
    {
        return new StatSufficiencyResultDto(
            result.RecipeId,
            result.MeetsMinimumRequirements,
            result.CraftsmanshipSufficient,
            result.ControlSufficient,
            result.LevelSufficient,
            result.CraftsmanshipDeficit,
            result.ControlDeficit,
            result.LevelDeficit,
            result.EstimatedHQPercent,
            result.EstimatedQualityPercent,
            result.EstimatedSteps,
            result.CanComplete,
            result.Recommendation.ToString(),
            result.SuggestedGearUpgrades.Select(ToDto).ToList());
    }

    public static GearUpgradeSuggestionDto ToDto(GearUpgradeSuggestion suggestion)
    {
        return new GearUpgradeSuggestionDto(
            suggestion.Slot,
            suggestion.CurrentItem,
            suggestion.SuggestedItem,
            suggestion.SuggestedEquipmentId,
            suggestion.CraftsmanshipGain,
            suggestion.ControlGain,
            suggestion.CPGain,
            suggestion.Source,
            suggestion.RecipeId);
    }

    public static ItemContextDto ToDto(ContextualLinks links)
    {
        return new ItemContextDto(
            links.ItemId,
            links.ItemName,
            links.IconUrl,
            links.Recipes.Select(ToDto).ToList(),
            links.CanSimulate,
            links.CanSolve,
            links.UsedInRecipes.Select(ToDto).ToList(),
            links.IsEquipment,
            links.EquipmentInfo is not null ? ToDto(links.EquipmentInfo) : null,
            links.GatheringInfo.Select(ToDto).ToList(),
            links.VendorSources.Select(ToDto).ToList());
    }

    public static RecipeLinkDto ToDto(RecipeLink link)
    {
        return new RecipeLinkDto(
            link.RecipeId,
            link.ResultItemName,
            link.IconUrl,
            link.CraftType,
            link.Level,
            link.Stars);
    }

    public static EquipmentLinkDto ToDto(EquipmentLink link)
    {
        return new EquipmentLinkDto(
            link.EquipmentId,
            link.EquipSlot,
            link.Craftsmanship,
            link.Control,
            link.CP,
            link.Gathering,
            link.Perception,
            link.GP);
    }

    public static GatheringLinkDto ToDto(GatheringLink link)
    {
        return new GatheringLinkDto(
            link.Zone,
            link.Coordinates,
            link.GatheringType,
            link.GatheringClass,
            link.GatheringLevel,
            link.IsTimed,
            link.SpawnTimes);
    }

    public static VendorLinkDto ToDto(VendorLink link)
    {
        return new VendorLinkDto(
            link.NpcName,
            link.Zone,
            link.Cost,
            link.Currency);
    }
}
