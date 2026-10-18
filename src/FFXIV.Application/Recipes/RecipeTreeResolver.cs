using FFXIV.Domain.Entities;
using FFXIV.Domain.Enums;
using FFXIV.Domain.Models;
using FFXIV.Domain.Repositories;

namespace FFXIV.Application.Recipes;

public sealed class RecipeTreeResolver : IRecipeTreeResolver
{
    private readonly IRecipeRepository _recipeRepo;
    private readonly IItemRepository _itemRepo;
    private readonly IItemSourceRepository _itemSourceRepo;
    private readonly IGatheringInfoRepository _gatheringRepo;
    private readonly IVendorInfoRepository _vendorRepo;

    private const int CrystalMinId = 2;
    private const int CrystalMaxId = 19;
    private const int MaxDepth = 20;

    public RecipeTreeResolver(
        IRecipeRepository recipeRepo,
        IItemRepository itemRepo,
        IItemSourceRepository itemSourceRepo,
        IGatheringInfoRepository gatheringRepo,
        IVendorInfoRepository vendorRepo)
    {
        _recipeRepo = recipeRepo;
        _itemRepo = itemRepo;
        _itemSourceRepo = itemSourceRepo;
        _gatheringRepo = gatheringRepo;
        _vendorRepo = vendorRepo;
    }

    public async Task<MaterialTreeResult> ResolveTreeAsync(int itemId, int quantity = 1, CancellationToken ct = default)
    {
        // State for the resolution pass
        var recipeCache = new Dictionary<int, Recipe?>();
        var ancestorStack = new HashSet<int>();
        var allItemIds = new HashSet<int>();

        // Look up the item to get its name/icon
        var rootItem = await _itemRepo.GetByIdAsync(itemId, ct);
        if (rootItem is null)
        {
            return CreateLeafResult(itemId, "Unknown Item", string.Empty, quantity);
        }

        // Check if a recipe exists for this item
        var rootRecipe = await _recipeRepo.GetByResultItemIdAsync(itemId, ct);
        if (rootRecipe is null)
        {
            // Not craftable — return a single leaf node result
            return CreateLeafResult(itemId, rootItem.Name, rootItem.IconPath, quantity);
        }

        recipeCache[itemId] = rootRecipe;

        // Build the tree
        var rootNode = await BuildNodeAsync(itemId, quantity, 0, ancestorStack, recipeCache, allItemIds, ct);

        // Batch-load source data for all visited items
        var sourcesByItem = await _itemSourceRepo.GetByItemIdsAsync(allItemIds, ct);
        var gatheringByItem = await _gatheringRepo.GetByItemIdsAsync(allItemIds, ct);
        var vendorByItem = await _vendorRepo.GetByItemIdsAsync(allItemIds, ct);

        // Walk tree to attach source info and build aggregated lists
        var shoppingDict = new Dictionary<int, ShoppingListItem>();
        var intermediateCrafts = new List<(int Depth, ShoppingListItem Item)>();
        var maxDepth = 0;

        AttachSourcesAndAggregate(
            rootNode, sourcesByItem, gatheringByItem, vendorByItem,
            shoppingDict, intermediateCrafts, ref maxDepth);

        // Separate crystals from the shopping list
        var crystals = new List<ShoppingListItem>();
        var shopping = new List<ShoppingListItem>();

        foreach (var item in shoppingDict.Values)
        {
            if (IsCrystal(item.ItemId))
            {
                item.SortCategory = "Crystals";
                crystals.Add(item);
            }
            else
            {
                shopping.Add(item);
            }
        }

        // Sort categories: Gathered, Vendor, Mob Drop, Other — alphabetically within each
        crystals.Sort((a, b) => string.Compare(a.ItemName, b.ItemName, StringComparison.OrdinalIgnoreCase));
        shopping.Sort((a, b) =>
        {
            var catCmp = string.Compare(a.SortCategory, b.SortCategory, StringComparison.OrdinalIgnoreCase);
            return catCmp != 0 ? catCmp : string.Compare(a.ItemName, b.ItemName, StringComparison.OrdinalIgnoreCase);
        });

        // Sort intermediate crafts deepest-first (reverse topological order)
        // OrderBy is stable, so equal depths keep first-occurrence order (List.Sort would not).
        var intermediates = intermediateCrafts.OrderByDescending(x => x.Depth).Select(x => x.Item).ToList();

        return new MaterialTreeResult
        {
            RootItem = rootNode,
            ShoppingList = shopping,
            TotalUniqueItems = shoppingDict.Count + intermediateCrafts.Count,
            TreeDepth = maxDepth,
            CrystalRequirements = crystals,
            IntermediateCrafts = intermediates
        };
    }

    private async Task<MaterialTreeNode> BuildNodeAsync(
        int itemId,
        int quantity,
        int depth,
        HashSet<int> ancestorStack,
        Dictionary<int, Recipe?> recipeCache,
        HashSet<int> allItemIds,
        CancellationToken ct)
    {
        allItemIds.Add(itemId);

        // Look up item info — try from recipe cache first (ResultItem nav property)
        var (itemName, iconUrl) = GetItemInfo(itemId, recipeCache);
        if (itemName == string.Empty)
        {
            var item = await _itemRepo.GetByIdAsync(itemId, ct);
            itemName = item?.Name ?? $"Item {itemId}";
            iconUrl = item?.IconPath ?? string.Empty;
        }

        var node = new MaterialTreeNode
        {
            ItemId = itemId,
            ItemName = itemName,
            IconUrl = iconUrl,
            Quantity = quantity,
            Depth = depth
        };

        // Crystal check — always terminal
        if (IsCrystal(itemId))
        {
            node.IsRawMaterial = true;
            node.SourceType = ItemSourceType.Gathered;
            return node;
        }

        // Depth safety check
        if (depth > MaxDepth)
        {
            node.IsRawMaterial = true;
            return node;
        }

        // Circular reference check
        if (ancestorStack.Contains(itemId))
        {
            node.IsRawMaterial = true;
            return node;
        }

        // Recipe lookup
        if (!recipeCache.TryGetValue(itemId, out var recipe))
        {
            // Single item lookup (will be batch-prefetched for siblings)
            var recipes = await _recipeRepo.GetByResultItemIdsAsync([itemId], ct);
            recipe = recipes.Count > 0 ? recipes[0] : null;
            recipeCache[itemId] = recipe;
        }

        if (recipe is null)
        {
            // Raw material — no recipe exists
            node.IsRawMaterial = true;
            return node;
        }

        // Yield-aware craft count
        var yield = Math.Max(recipe.Yield, 1);
        var craftsNeeded = (quantity + yield - 1) / yield;

        node.IsRawMaterial = false;
        node.SourceType = ItemSourceType.Crafted;
        node.Recipe = new RecipeSummary
        {
            RecipeId = recipe.RecipeId,
            CraftType = recipe.CraftType,
            RecipeLevel = recipe.RecipeLevel,
            CraftsmanshipRequired = recipe.RequiredCraftsmanship,
            ControlRequired = recipe.RequiredControl,
            YieldPerCraft = yield
        };

        // Batch prefetch recipes for all ingredients not yet in cache
        var ingredientIds = recipe.Ingredients
            .Select(i => i.ItemId)
            .Where(id => !recipeCache.ContainsKey(id) && !IsCrystal(id))
            .ToList();

        if (ingredientIds.Count > 0)
        {
            var childRecipes = await _recipeRepo.GetByResultItemIdsAsync(ingredientIds, ct);
            // Index by ResultItemId — take first recipe per item
            foreach (var cr in childRecipes)
            {
                recipeCache.TryAdd(cr.ResultItemId, cr);
            }
            // Mark items with no recipe as null in cache
            foreach (var id in ingredientIds)
            {
                recipeCache.TryAdd(id, null);
            }
        }

        // Recurse into ingredients
        ancestorStack.Add(itemId);

        foreach (var ingredient in recipe.Ingredients)
        {
            var childQty = ingredient.Quantity * craftsNeeded;

            // Store item info from ingredient's nav property for child lookup
            if (ingredient.Item is not null)
            {
                // Make sure we can find it when building child nodes
                allItemIds.Add(ingredient.ItemId);
            }

            var child = await BuildNodeAsync(
                ingredient.ItemId, childQty, depth + 1,
                ancestorStack, recipeCache, allItemIds, ct);

            node.Children.Add(child);
        }

        ancestorStack.Remove(itemId);
        return node;
    }

    private static void AttachSourcesAndAggregate(
        MaterialTreeNode node,
        Dictionary<int, List<ItemSource>> sourcesByItem,
        Dictionary<int, List<GatheringInfo>> gatheringByItem,
        Dictionary<int, List<VendorInfo>> vendorByItem,
        Dictionary<int, ShoppingListItem> shoppingDict,
        List<(int Depth, ShoppingListItem Item)> intermediateCrafts,
        ref int maxDepth)
    {
        if (node.Depth > maxDepth)
            maxDepth = node.Depth;

        // Build source summaries for this node
        var sources = BuildSourceSummaries(node.ItemId, sourcesByItem, gatheringByItem, vendorByItem);
        node.Sources = sources;

        // Set SourceType from source data if this is a raw material
        if (node.IsRawMaterial && sources.Count > 0)
        {
            node.SourceType = PickPrimarySourceType(sources);
        }

        if (node.IsRawMaterial)
        {
            // Aggregate into shopping list
            if (shoppingDict.TryGetValue(node.ItemId, out var existing))
            {
                existing.TotalQuantity += node.Quantity;
            }
            else
            {
                shoppingDict[node.ItemId] = new ShoppingListItem
                {
                    ItemId = node.ItemId,
                    ItemName = node.ItemName,
                    IconUrl = node.IconUrl,
                    TotalQuantity = node.Quantity,
                    SourceType = node.SourceType,
                    Sources = sources,
                    SortCategory = GetSortCategory(node.ItemId, node.SourceType)
                };
            }
        }
        else
        {
            // Non-root intermediate craft (depth > 0 means it's not the final item)
            if (node.Depth > 0)
            {
                // Check if already tracked
                var existingIntermediate = intermediateCrafts.FirstOrDefault(x => x.Item.ItemId == node.ItemId);
                if (existingIntermediate.Item is not null)
                {
                    existingIntermediate.Item.TotalQuantity += node.Quantity;
                }
                else
                {
                    intermediateCrafts.Add((node.Depth, new ShoppingListItem
                    {
                        ItemId = node.ItemId,
                        ItemName = node.ItemName,
                        IconUrl = node.IconUrl,
                        TotalQuantity = node.Quantity,
                        SourceType = ItemSourceType.Crafted,
                        Sources = sources,
                        SortCategory = "Crafted"
                    }));
                }
            }

            // Recurse into children
            foreach (var child in node.Children)
            {
                AttachSourcesAndAggregate(child, sourcesByItem, gatheringByItem, vendorByItem,
                    shoppingDict, intermediateCrafts, ref maxDepth);
            }
        }
    }

    private static List<ItemSourceSummary> BuildSourceSummaries(
        int itemId,
        Dictionary<int, List<ItemSource>> sourcesByItem,
        Dictionary<int, List<GatheringInfo>> gatheringByItem,
        Dictionary<int, List<VendorInfo>> vendorByItem)
    {
        var summaries = new List<ItemSourceSummary>();

        if (gatheringByItem.TryGetValue(itemId, out var gatheringInfos))
        {
            foreach (var g in gatheringInfos)
            {
                var desc = $"{FormatGatheringVerb(g.GatheringType)} in {g.ZoneName} (Lv. {g.Level} {g.ClassJob})";
                summaries.Add(new ItemSourceSummary
                {
                    SourceType = ItemSourceType.Gathered,
                    Description = desc,
                    ZoneName = g.ZoneName,
                    GatheringLevel = g.Level,
                    GatheringClass = g.ClassJob
                });
            }
        }

        if (vendorByItem.TryGetValue(itemId, out var vendorInfos))
        {
            foreach (var v in vendorInfos)
            {
                var desc = v.CurrencyType == "Gil"
                    ? $"Sold by {v.NpcName} for {v.Cost} Gil"
                    : $"Purchased with {v.CurrencyType} ({v.Cost})";
                summaries.Add(new ItemSourceSummary
                {
                    SourceType = ItemSourceType.Vendor,
                    Description = desc,
                    ZoneName = v.ZoneName,
                    NpcName = v.NpcName,
                    Cost = v.Cost,
                    CurrencyType = v.CurrencyType
                });
            }
        }

        // Add any other source types from ItemSource
        if (sourcesByItem.TryGetValue(itemId, out var itemSources))
        {
            foreach (var s in itemSources)
            {
                // Avoid duplicating Gathered/Vendor which we already built from detailed tables
                if (s.SourceType is ItemSourceType.Gathered or ItemSourceType.Vendor)
                    continue;

                summaries.Add(new ItemSourceSummary
                {
                    SourceType = s.SourceType,
                    Description = s.SourceType.ToString()
                });
            }
        }

        return summaries;
    }

    private static ItemSourceType PickPrimarySourceType(List<ItemSourceSummary> sources)
    {
        // Priority: Gathered > Vendor > Crafted > everything else
        if (sources.Any(s => s.SourceType == ItemSourceType.Gathered))
            return ItemSourceType.Gathered;
        if (sources.Any(s => s.SourceType == ItemSourceType.Vendor))
            return ItemSourceType.Vendor;
        if (sources.Any(s => s.SourceType == ItemSourceType.Crafted))
            return ItemSourceType.Crafted;
        return sources[0].SourceType;
    }

    private static string GetSortCategory(int itemId, ItemSourceType sourceType)
    {
        if (IsCrystal(itemId))
            return "Crystals";

        return sourceType switch
        {
            ItemSourceType.Gathered => "Gathered",
            ItemSourceType.Vendor => "Vendor",
            ItemSourceType.MobDrop => "Mob Drop",
            _ => "Other"
        };
    }

    private static string FormatGatheringVerb(GatheringType type)
    {
        return type switch
        {
            GatheringType.Mining or GatheringType.Quarrying => "Mined",
            GatheringType.Logging or GatheringType.Harvesting => "Harvested",
            GatheringType.Fishing => "Fished",
            _ => "Gathered"
        };
    }

    private static bool IsCrystal(int itemId) => itemId is >= CrystalMinId and <= CrystalMaxId;

    private static (string Name, string IconUrl) GetItemInfo(int itemId, Dictionary<int, Recipe?> recipeCache)
    {
        // Try to find item info from any recipe's ingredient nav properties in the cache
        foreach (var recipe in recipeCache.Values)
        {
            if (recipe is null) continue;

            if (recipe.ResultItemId == itemId && recipe.ResultItem is not null)
                return (recipe.ResultItem.Name, recipe.ResultItem.IconPath);

            foreach (var ing in recipe.Ingredients)
            {
                if (ing.ItemId == itemId && ing.Item is not null)
                    return (ing.Item.Name, ing.Item.IconPath);
            }
        }

        return (string.Empty, string.Empty);
    }

    private static MaterialTreeResult CreateLeafResult(int itemId, string name, string iconUrl, int quantity)
    {
        var leaf = new MaterialTreeNode
        {
            ItemId = itemId,
            ItemName = name,
            IconUrl = iconUrl,
            Quantity = quantity,
            IsRawMaterial = true,
            Depth = 0
        };

        var shoppingItem = new ShoppingListItem
        {
            ItemId = itemId,
            ItemName = name,
            IconUrl = iconUrl,
            TotalQuantity = quantity,
            SortCategory = IsCrystal(itemId) ? "Crystals" : "Other"
        };

        return new MaterialTreeResult
        {
            RootItem = leaf,
            ShoppingList = IsCrystal(itemId) ? [] : [shoppingItem],
            TotalUniqueItems = 1,
            TreeDepth = 0,
            CrystalRequirements = IsCrystal(itemId) ? [shoppingItem] : [],
            IntermediateCrafts = []
        };
    }
}
