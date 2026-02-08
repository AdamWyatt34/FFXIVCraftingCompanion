using System.Collections.Concurrent;
using System.Diagnostics;
using FFXIV.Domain.Interfaces;
using FFXIV.Domain.Models;
using FFXIV.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace FFXIV.Application.Recipes;

public sealed class TreeValidationService : ITreeValidationService
{
    private readonly IRecipeTreeResolver _treeResolver;
    private readonly IGarlandToolsClient _garlandClient;
    private readonly IRecipeRepository _recipeRepo;
    private readonly ILogger<TreeValidationService> _logger;

    public TreeValidationService(
        IRecipeTreeResolver treeResolver,
        IGarlandToolsClient garlandClient,
        IRecipeRepository recipeRepo,
        ILogger<TreeValidationService> logger)
    {
        _treeResolver = treeResolver;
        _garlandClient = garlandClient;
        _recipeRepo = recipeRepo;
        _logger = logger;
    }

    public async Task<TreeValidationResult> ValidateTreeAsync(
        int recipeId, int quantity = 1, CancellationToken ct = default)
    {
        return await ValidateTreeInternalAsync(recipeId, quantity, new ConcurrentDictionary<int, GarlandItemData?>(), ct);
    }

    public async Task<BatchValidationResult> ValidateBatchAsync(
        IEnumerable<int> recipeIds, CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var batch = new BatchValidationResult();
        var sessionCache = new ConcurrentDictionary<int, GarlandItemData?>();

        foreach (var recipeId in recipeIds)
        {
            ct.ThrowIfCancellationRequested();
            var result = await ValidateTreeInternalAsync(recipeId, 1, sessionCache, ct);
            batch.Results.Add(result);
            batch.TotalValidated++;

            if (result.ErrorMessage is not null)
                batch.ErrorCount++;
            else if (result.IsValid)
                batch.PassedCount++;
            else
                batch.FailedCount++;

            if (batch.TotalValidated % 10 == 0)
                _logger.LogInformation("Validated {Count} recipes so far...", batch.TotalValidated);
        }

        batch.TotalDuration = stopwatch.Elapsed;

        _logger.LogInformation(
            "Batch validation complete: {Total} validated, {Passed} passed, {Failed} failed, {Errors} errors in {Duration}",
            batch.TotalValidated, batch.PassedCount, batch.FailedCount, batch.ErrorCount, batch.TotalDuration);

        return batch;
    }

    private async Task<TreeValidationResult> ValidateTreeInternalAsync(
        int recipeId, int quantity,
        ConcurrentDictionary<int, GarlandItemData?> sessionCache,
        CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new TreeValidationResult { RecipeId = recipeId };

        try
        {
            var recipe = await _recipeRepo.GetByIdAsync(recipeId, ct);
            if (recipe is null)
            {
                result.ErrorMessage = $"Recipe {recipeId} not found in database";
                result.ValidationDuration = stopwatch.Elapsed;
                return result;
            }

            result.ItemId = recipe.ResultItemId;
            result.ItemName = recipe.ResultItem?.Name ?? $"Item {recipe.ResultItemId}";

            var ourTree = await _treeResolver.ResolveTreeAsync(recipe.ResultItemId, quantity, ct);

            var garlandMaterials = new Dictionary<int, (string Name, int Quantity)>();
            await BuildGarlandMaterialListAsync(
                recipe.ResultItemId, quantity, garlandMaterials,
                new HashSet<int>(), sessionCache, ct);

            var ourMaterials = new Dictionary<int, (string Name, int Quantity)>();
            foreach (var item in ourTree.ShoppingList)
                ourMaterials[item.ItemId] = (item.ItemName, item.TotalQuantity);
            foreach (var crystal in ourTree.CrystalRequirements)
                ourMaterials[crystal.ItemId] = (crystal.ItemName, crystal.TotalQuantity);

            result.Discrepancies = CompareMaterialLists(ourMaterials, garlandMaterials);
            result.IsValid = result.Discrepancies.Count == 0;
            result.OurTotalRawMaterials = ourMaterials.Values.Sum(m => m.Quantity);
            result.GarlandTotalRawMaterials = garlandMaterials.Values.Sum(m => m.Quantity);

            if (result.IsValid)
                _logger.LogInformation("Recipe {RecipeId} ({ItemName}) validated successfully", recipeId, result.ItemName);
            else
                _logger.LogWarning("Recipe {RecipeId} ({ItemName}) has {Count} discrepancies",
                    recipeId, result.ItemName, result.Discrepancies.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Validation failed for recipe {RecipeId}", recipeId);
            result.ErrorMessage = ex.Message;
        }

        result.ValidationDuration = stopwatch.Elapsed;
        return result;
    }

    private async Task BuildGarlandMaterialListAsync(
        int itemId, int quantity,
        Dictionary<int, (string Name, int Quantity)> materials,
        HashSet<int> ancestors,
        ConcurrentDictionary<int, GarlandItemData?> sessionCache,
        CancellationToken ct)
    {
        if (!ancestors.Add(itemId))
        {
            AddToMaterials(materials, itemId, $"Item {itemId} (cycle)", quantity);
            return;
        }

        try
        {
            var garlandData = await GetOrFetchAsync(itemId, sessionCache, ct);
            if (garlandData?.Item is null)
            {
                AddToMaterials(materials, itemId, $"Item {itemId}", quantity);
                return;
            }

            var item = garlandData.Item;

            if (item.Craft is null || item.Craft.Count == 0)
            {
                AddToMaterials(materials, itemId, item.Name, quantity);
                return;
            }

            var craft = item.Craft[0];
            var yield = Math.Max(craft.Yield, 1);
            var craftsNeeded = (quantity + yield - 1) / yield;

            var ingredientNames = new Dictionary<int, string>();
            if (garlandData.Ingredients is not null)
            {
                foreach (var ing in garlandData.Ingredients)
                    ingredientNames[ing.Id] = ing.Name;
            }

            foreach (var ingredient in craft.Ingredients)
            {
                var childQty = ingredient.Amount * craftsNeeded;
                await BuildGarlandMaterialListAsync(
                    ingredient.Id, childQty, materials,
                    ancestors, sessionCache, ct);
            }
        }
        finally
        {
            ancestors.Remove(itemId);
        }
    }

    private async Task<GarlandItemData?> GetOrFetchAsync(
        int itemId,
        ConcurrentDictionary<int, GarlandItemData?> sessionCache,
        CancellationToken ct)
    {
        if (sessionCache.TryGetValue(itemId, out var cached))
            return cached;

        var data = await _garlandClient.GetItemAsync(itemId, ct);
        sessionCache.TryAdd(itemId, data);
        return data;
    }

    private static void AddToMaterials(
        Dictionary<int, (string Name, int Quantity)> dict,
        int itemId, string name, int quantity)
    {
        if (dict.TryGetValue(itemId, out var existing))
            dict[itemId] = (existing.Name, existing.Quantity + quantity);
        else
            dict[itemId] = (name, quantity);
    }

    private static List<TreeDiscrepancy> CompareMaterialLists(
        Dictionary<int, (string Name, int Quantity)> ours,
        Dictionary<int, (string Name, int Quantity)> garland)
    {
        var discrepancies = new List<TreeDiscrepancy>();
        var allIds = new HashSet<int>(ours.Keys);
        allIds.UnionWith(garland.Keys);

        foreach (var id in allIds)
        {
            var ourHas = ours.TryGetValue(id, out var ourEntry);
            var garlandHas = garland.TryGetValue(id, out var garlandEntry);

            if (ourHas && garlandHas)
            {
                if (ourEntry.Quantity != garlandEntry.Quantity)
                {
                    discrepancies.Add(new TreeDiscrepancy
                    {
                        ItemId = id,
                        ItemName = ourEntry.Name,
                        OurQuantity = ourEntry.Quantity,
                        GarlandQuantity = garlandEntry.Quantity,
                        DiscrepancyType = "QuantityMismatch"
                    });
                }
            }
            else if (ourHas)
            {
                discrepancies.Add(new TreeDiscrepancy
                {
                    ItemId = id,
                    ItemName = ourEntry.Name,
                    OurQuantity = ourEntry.Quantity,
                    GarlandQuantity = 0,
                    DiscrepancyType = "Extra"
                });
            }
            else
            {
                discrepancies.Add(new TreeDiscrepancy
                {
                    ItemId = id,
                    ItemName = garlandEntry.Name,
                    OurQuantity = 0,
                    GarlandQuantity = garlandEntry.Quantity,
                    DiscrepancyType = "Missing"
                });
            }
        }

        return discrepancies;
    }
}
