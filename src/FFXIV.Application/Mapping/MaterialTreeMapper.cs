using FFXIV.Application.Recipes.DTOs;
using FFXIV.Domain.Enums;
using FFXIV.Domain.Models;

namespace FFXIV.Application.Mapping;

public static class MaterialTreeMapper
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

    public static MaterialTreeResultDto ToDto(MaterialTreeResult result, int requestedQuantity)
    {
        var intermediateRecipeLookup = new Dictionary<int, RecipeSummary>();
        CollectIntermediateRecipes(result.RootItem, intermediateRecipeLookup, isRoot: true);

        var tree = MapNode(result.RootItem);

        var shoppingList = result.ShoppingList
            .Select(MapShoppingListItem)
            .ToList();

        var crystals = result.CrystalRequirements
            .Select(MapShoppingListItem)
            .ToList();

        var intermediates = MapIntermediateCrafts(result.IntermediateCrafts, intermediateRecipeLookup);

        var summary = new TreeSummaryDto(
            TotalUniqueRawMaterials: result.ShoppingList.Count,
            TotalUniqueCrystals: result.CrystalRequirements.Count,
            TotalIntermediateCrafts: result.IntermediateCrafts.Count,
            MaxTreeDepth: result.TreeDepth,
            RequestedQuantity: requestedQuantity);

        return new MaterialTreeResultDto(
            Tree: tree,
            ShoppingList: shoppingList,
            IntermediateCrafts: intermediates,
            CrystalRequirements: crystals,
            Summary: summary);
    }

    public static MaterialSummaryDto ToSummary(MaterialTreeResult result)
    {
        return new MaterialSummaryDto(
            RawMaterialCount: result.ShoppingList.Count,
            IntermediateCraftCount: result.IntermediateCrafts.Count,
            CrystalTypeCount: result.CrystalRequirements.Count,
            TreeDepth: result.TreeDepth);
    }

    private static MaterialTreeNodeDto MapNode(MaterialTreeNode node)
    {
        return new MaterialTreeNodeDto(
            ItemId: node.ItemId,
            ItemName: node.ItemName,
            IconUrl: node.IconUrl,
            Quantity: node.Quantity,
            IsRawMaterial: node.IsRawMaterial,
            PrimarySource: GetSourceTypeDisplay(node.SourceType),
            Sources: node.Sources.Select(MapSource).ToList(),
            Recipe: node.Recipe is not null ? MapRecipeSummary(node.Recipe) : null,
            Children: node.Children.Select(MapNode).ToList(),
            Depth: node.Depth);
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

    private static RecipeSummaryDto MapRecipeSummary(RecipeSummary recipe)
    {
        return new RecipeSummaryDto(
            RecipeId: recipe.RecipeId,
            CraftType: recipe.CraftType.ToString(),
            CraftTypeName: GetCraftTypeDisplay(recipe.CraftType),
            RecipeLevel: recipe.RecipeLevel,
            RequiredCraftsmanship: recipe.CraftsmanshipRequired,
            RequiredControl: recipe.ControlRequired,
            Yield: recipe.YieldPerCraft);
    }

    private static ShoppingListItemDto MapShoppingListItem(ShoppingListItem item)
    {
        var primarySource = GetSourceTypeDisplay(item.SourceType);
        var alsoVendor = item.SourceType != ItemSourceType.Vendor
            && item.Sources.Any(s => s.SourceType == ItemSourceType.Vendor);

        return new ShoppingListItemDto(
            ItemId: item.ItemId,
            ItemName: item.ItemName,
            IconUrl: item.IconUrl,
            TotalQuantity: item.TotalQuantity,
            PrimarySource: primarySource,
            Sources: item.Sources.Select(MapSource).ToList(),
            Category: item.SortCategory,
            AlsoAvailableFromVendor: alsoVendor);
    }

    private static List<IntermediateCraftDto> MapIntermediateCrafts(
        List<ShoppingListItem> intermediates,
        Dictionary<int, RecipeSummary> recipeLookup)
    {
        var result = new List<IntermediateCraftDto>();
        for (var i = 0; i < intermediates.Count; i++)
        {
            var item = intermediates[i];
            if (!recipeLookup.TryGetValue(item.ItemId, out var recipe))
                continue;

            result.Add(new IntermediateCraftDto(
                ItemId: item.ItemId,
                ItemName: item.ItemName,
                IconUrl: item.IconUrl,
                CraftType: recipe.CraftType.ToString(),
                CraftTypeName: GetCraftTypeDisplay(recipe.CraftType),
                RecipeId: recipe.RecipeId,
                Quantity: item.TotalQuantity,
                RecipeLevel: recipe.RecipeLevel,
                CraftOrder: i + 1));
        }
        return result;
    }

    private static void CollectIntermediateRecipes(
        MaterialTreeNode node,
        Dictionary<int, RecipeSummary> lookup,
        bool isRoot = false)
    {
        if (!isRoot && !node.IsRawMaterial && node.Recipe is not null)
        {
            lookup.TryAdd(node.ItemId, node.Recipe);
        }

        foreach (var child in node.Children)
        {
            CollectIntermediateRecipes(child, lookup);
        }
    }

    private static string GetCraftTypeDisplay(CraftType type)
        => CraftTypeDisplayNames.GetValueOrDefault(type, type.ToString());

    private static string GetSourceTypeDisplay(ItemSourceType type)
        => SourceTypeDisplayNames.GetValueOrDefault(type, type.ToString());
}
