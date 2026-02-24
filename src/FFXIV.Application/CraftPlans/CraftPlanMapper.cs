using System.Text.Json;
using FFXIV.Application.CraftPlans.DTOs;
using FFXIV.Application.Recipes.DTOs;
using FFXIV.Domain.Entities;
using FFXIV.Domain.Enums;
using FFXIV.Domain.Models;

namespace FFXIV.Application.CraftPlans;

public static class CraftPlanMapper
{
    private static readonly Dictionary<CraftType, string> CraftTypeDisplayNames = new()
    {
        [CraftType.CRP] = "Carpenter",
        [CraftType.BSM] = "Blacksmith",
        [CraftType.ARM] = "Armorer",
        [CraftType.GSM] = "Goldsmith",
        [CraftType.LTW] = "Leatherworker",
        [CraftType.WVR] = "Weaver",
        [CraftType.ALC] = "Alchemist",
        [CraftType.CUL] = "Culinarian"
    };

    private static readonly Dictionary<ItemSourceType, string> SourceTypeDisplayNames = new()
    {
        [ItemSourceType.Gathered] = "Gathered",
        [ItemSourceType.Vendor] = "Vendor",
        [ItemSourceType.Crafted] = "Crafted",
        [ItemSourceType.MobDrop] = "Mob Drop",
        [ItemSourceType.Scrip] = "Scrip Exchange",
        [ItemSourceType.Quest] = "Quest Reward",
        [ItemSourceType.Dungeon] = "Dungeon Drop",
        [ItemSourceType.Desynth] = "Desynthesis",
        [ItemSourceType.Reduction] = "Aetherial Reduction",
        [ItemSourceType.Other] = "Other"
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static CraftPlanResultDto ToResultDto(CraftPlanResult result)
    {
        return new CraftPlanResultDto(
            Items: result.Items.Select(i => new CraftPlanItemResultDto(
                i.RecipeId, i.ItemId, i.ItemName, i.IconUrl, i.Quantity, i.CraftType)).ToList(),
            RawMaterials: result.RawMaterials.Select(MapMaterialDto).ToList(),
            Crystals: result.Crystals.Select(MapMaterialDto).ToList(),
            IntermediateCrafts: result.IntermediateCrafts.Select(MapIntermediateDto).ToList(),
            Summary: new CraftPlanSummaryDto(
                result.Summary.TotalPlanItems,
                result.Summary.TotalUniqueRawMaterials,
                result.Summary.TotalUniqueCrystals,
                result.Summary.TotalIntermediateCrafts,
                result.Summary.MaxTreeDepth));
    }

    public static CraftPlanDto ToDto(CraftPlan entity)
    {
        var items = JsonSerializer.Deserialize<List<CraftPlanItemInput>>(entity.Items, JsonOptions) ?? [];
        return new CraftPlanDto(entity.Id, entity.Name, items, entity.CreatedAt, entity.UpdatedAt);
    }

    public static string SerializeItems(List<CraftPlanItemInput> items)
    {
        return JsonSerializer.Serialize(items, JsonOptions);
    }

    public static List<CraftPlanInput> ToInputs(List<CraftPlanItemInput> items)
    {
        return items.Select(i => new CraftPlanInput(i.RecipeId, i.Quantity)).ToList();
    }

    private static AggregatedMaterialDto MapMaterialDto(AggregatedMaterial material)
    {
        return new AggregatedMaterialDto(
            ItemId: material.ItemId,
            ItemName: material.ItemName,
            IconUrl: material.IconUrl,
            TotalQuantity: material.TotalQuantity,
            PrimarySource: GetSourceTypeDisplay(material.PrimarySourceType),
            Sources: material.Sources.Select(MapSource).ToList(),
            Category: material.SortCategory,
            Contributions: material.Contributions.Select(c =>
                new MaterialContributionDto(c.RecipeId, c.ItemName, c.Quantity)).ToList());
    }

    private static AggregatedIntermediateCraftDto MapIntermediateDto(AggregatedIntermediateCraft craft)
    {
        return new AggregatedIntermediateCraftDto(
            ItemId: craft.ItemId,
            ItemName: craft.ItemName,
            IconUrl: craft.IconUrl,
            TotalQuantity: craft.TotalQuantity,
            RecipeId: craft.RecipeId,
            CraftType: craft.CraftType.ToString(),
            CraftTypeName: GetCraftTypeDisplay(craft.CraftType),
            RecipeLevel: craft.RecipeLevel,
            YieldPerCraft: craft.YieldPerCraft,
            CraftOrder: craft.CraftOrder);
    }

    private static ItemSourceDto MapSource(ItemSourceSummary source)
    {
        return new ItemSourceDto(
            Type: GetSourceTypeDisplay(source.SourceType),
            Description: source.Description,
            Zone: source.ZoneName,
            Npc: source.NpcName,
            Cost: source.Cost,
            Currency: source.CurrencyType,
            GatheringLevel: source.GatheringLevel,
            GatheringClass: source.GatheringClass);
    }

    private static string GetCraftTypeDisplay(CraftType type)
        => CraftTypeDisplayNames.GetValueOrDefault(type, type.ToString());

    private static string GetSourceTypeDisplay(ItemSourceType type)
        => SourceTypeDisplayNames.GetValueOrDefault(type, type.ToString());
}
