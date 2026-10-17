using FFXIV.Application.Recipes;
using FFXIV.Domain.Enums;
using FFXIV.Domain.Models;
using FFXIV.Domain.Repositories;

namespace FFXIV.Application.CraftPlans;

public sealed class CraftPlanResolver : ICraftPlanResolver
{
    private readonly IRecipeRepository _recipeRepo;
    private readonly IRecipeTreeResolver _treeResolver;

    public CraftPlanResolver(IRecipeRepository recipeRepo, IRecipeTreeResolver treeResolver)
    {
        _recipeRepo = recipeRepo;
        _treeResolver = treeResolver;
    }

    public async Task<CraftPlanResult?> ResolveAsync(List<CraftPlanInput> items, CancellationToken ct = default)
    {
        // 1. Validate — look up each recipe
        var recipes = new List<(CraftPlanInput Input, Domain.Entities.Recipe Recipe)>();
        foreach (var input in items)
        {
            var recipe = await _recipeRepo.GetByIdAsync(input.RecipeId, ct);
            if (recipe is null)
                return null;
            recipes.Add((input, recipe));
        }

        // 2. Resolve trees for each plan item
        var trees = new List<(CraftPlanInput Input, Domain.Entities.Recipe Recipe, MaterialTreeResult Tree)>();
        foreach (var (input, recipe) in recipes)
        {
            var tree = await _treeResolver.ResolveTreeAsync(recipe.ResultItemId, input.Quantity, ct);
            trees.Add((input, recipe, tree));
        }

        // 3. Build plan item results
        var planItems = trees.Select(t => new CraftPlanItemResult
        {
            RecipeId = t.Recipe.RecipeId,
            ItemId = t.Recipe.ResultItemId,
            ItemName = t.Tree.RootItem.ItemName,
            IconUrl = t.Tree.RootItem.IconUrl,
            Quantity = t.Input.Quantity,
            CraftType = t.Recipe.CraftType.ToString()
        }).ToList();

        // 4. Aggregate raw materials
        var rawMaterials = new Dictionary<int, AggregatedMaterial>();
        foreach (var (input, recipe, tree) in trees)
        {
            foreach (var item in tree.ShoppingList)
            {
                AggregateMaterial(rawMaterials, item, recipe.RecipeId, tree.RootItem.ItemName);
            }
        }

        // 5. Aggregate crystals
        var crystals = new Dictionary<int, AggregatedMaterial>();
        foreach (var (input, recipe, tree) in trees)
        {
            foreach (var item in tree.CrystalRequirements)
            {
                AggregateMaterial(crystals, item, recipe.RecipeId, tree.RootItem.ItemName);
            }
        }

        // 6. Collect intermediate recipe info from tree nodes
        var intermediateRecipes = new Dictionary<int, RecipeSummary>();
        var intermediateDepths = new Dictionary<int, int>();
        foreach (var (_, _, tree) in trees)
        {
            CollectIntermediateRecipes(tree.RootItem, intermediateRecipes, intermediateDepths, isRoot: true);
        }

        // 7. Aggregate intermediate craft quantities
        var intermediates = new Dictionary<int, AggregatedIntermediateCraft>();
        foreach (var (input, recipe, tree) in trees)
        {
            foreach (var item in tree.IntermediateCrafts)
            {
                if (!intermediateRecipes.TryGetValue(item.ItemId, out var recipeSummary))
                    continue;

                if (intermediates.TryGetValue(item.ItemId, out var existing))
                {
                    existing.TotalQuantity += item.TotalQuantity;
                }
                else
                {
                    intermediates[item.ItemId] = new AggregatedIntermediateCraft
                    {
                        ItemId = item.ItemId,
                        ItemName = item.ItemName,
                        IconUrl = item.IconUrl,
                        TotalQuantity = item.TotalQuantity,
                        RecipeId = recipeSummary.RecipeId,
                        CraftType = recipeSummary.CraftType,
                        RecipeLevel = recipeSummary.RecipeLevel,
                        YieldPerCraft = recipeSummary.YieldPerCraft,
                        MaxDepth = intermediateDepths.GetValueOrDefault(item.ItemId, 1)
                    };
                }
            }
        }

        // 8. Sort intermediates by depth descending (deepest first), assign craft order
        // Ordinal, so the order does not depend on the server's culture (the frontend port
        // reproduces it).
        var sortedIntermediates = intermediates.Values
            .OrderByDescending(i => i.MaxDepth)
            .ThenBy(i => i.ItemName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        for (var i = 0; i < sortedIntermediates.Count; i++)
        {
            sortedIntermediates[i].CraftOrder = i + 1;
        }

        // Sort raw materials by category then name
        var sortedRaw = rawMaterials.Values
            .OrderBy(m => m.SortCategory, StringComparer.OrdinalIgnoreCase)
            .ThenBy(m => m.ItemName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Sort crystals by name
        var sortedCrystals = crystals.Values
            .OrderBy(m => m.ItemName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var maxDepth = trees.Max(t => t.Tree.TreeDepth);

        return new CraftPlanResult
        {
            Items = planItems,
            RawMaterials = sortedRaw,
            Crystals = sortedCrystals,
            IntermediateCrafts = sortedIntermediates,
            Summary = new CraftPlanSummary
            {
                TotalPlanItems = planItems.Count,
                TotalUniqueRawMaterials = sortedRaw.Count,
                TotalUniqueCrystals = sortedCrystals.Count,
                TotalIntermediateCrafts = sortedIntermediates.Count,
                MaxTreeDepth = maxDepth
            }
        };
    }

    private static void AggregateMaterial(
        Dictionary<int, AggregatedMaterial> lookup,
        ShoppingListItem item,
        int recipeId,
        string recipeItemName)
    {
        if (lookup.TryGetValue(item.ItemId, out var existing))
        {
            existing.TotalQuantity += item.TotalQuantity;
            existing.Contributions.Add(new MaterialContribution
            {
                RecipeId = recipeId,
                ItemName = recipeItemName,
                Quantity = item.TotalQuantity
            });
        }
        else
        {
            lookup[item.ItemId] = new AggregatedMaterial
            {
                ItemId = item.ItemId,
                ItemName = item.ItemName,
                IconUrl = item.IconUrl,
                TotalQuantity = item.TotalQuantity,
                PrimarySourceType = item.SourceType,
                Sources = item.Sources,
                SortCategory = item.SortCategory,
                Contributions =
                [
                    new MaterialContribution
                    {
                        RecipeId = recipeId,
                        ItemName = recipeItemName,
                        Quantity = item.TotalQuantity
                    }
                ]
            };
        }
    }

    private static void CollectIntermediateRecipes(
        MaterialTreeNode node,
        Dictionary<int, RecipeSummary> recipeLookup,
        Dictionary<int, int> depthLookup,
        bool isRoot = false)
    {
        if (!isRoot && !node.IsRawMaterial && node.Recipe is not null)
        {
            recipeLookup.TryAdd(node.ItemId, node.Recipe);

            // Track the maximum depth for this intermediate
            if (depthLookup.TryGetValue(node.ItemId, out var existingDepth))
            {
                if (node.Depth > existingDepth)
                    depthLookup[node.ItemId] = node.Depth;
            }
            else
            {
                depthLookup[node.ItemId] = node.Depth;
            }
        }

        foreach (var child in node.Children)
        {
            CollectIntermediateRecipes(child, recipeLookup, depthLookup);
        }
    }
}
