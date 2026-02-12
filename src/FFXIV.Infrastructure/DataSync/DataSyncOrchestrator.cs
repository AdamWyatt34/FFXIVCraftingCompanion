using System.Diagnostics;
using FFXIV.Domain.Entities;
using FFXIV.Domain.Interfaces;
using FFXIV.Infrastructure.ExternalApis;
using FFXIV.Infrastructure.ExternalApis.Models;
using FFXIV.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FFXIV.Infrastructure.DataSync;

public sealed class DataSyncOrchestrator
{
    private readonly AppDbContext _db;
    private readonly IXivApiClient _xivApiClient;
    private readonly DataSyncOptions _options;
    private readonly SourceDataSyncService _sourceDataSync;
    private readonly EquipmentSyncService _equipmentSync;
    private readonly GarlandDataEnricher _garlandEnricher;
    private readonly ICacheService _cache;
    private readonly ILogger<DataSyncOrchestrator> _logger;

    private const string ItemFields = "Name,LevelItem,CanBeHq,Icon,ItemSearchCategory,ItemUICategory,PriceMid";
    private const string RecipeFields = "ItemResult,CraftType,RecipeLevelTable,DifficultyFactor,QualityFactor,DurabilityFactor,AmountResult,CanHq,Ingredient,AmountIngredient";
    private const int ItemPageSize = 500;
    private const int RecipePageSize = 50;
    private const string RecipeLevelTableFieldList = "ClassJobLevel,Stars,Difficulty,Quality,Durability,SuggestedCraftsmanship,SuggestedControl,ProgressDivider,ProgressModifier,QualityDivider,QualityModifier";
    private const int RecipeLevelTablePageSize = 500;

    public DataSyncOrchestrator(
        AppDbContext db,
        IXivApiClient xivApiClient,
        IOptions<DataSyncOptions> options,
        SourceDataSyncService sourceDataSync,
        EquipmentSyncService equipmentSync,
        GarlandDataEnricher garlandEnricher,
        ICacheService cache,
        ILogger<DataSyncOrchestrator> logger)
    {
        _db = db;
        _xivApiClient = xivApiClient;
        _options = options.Value;
        _sourceDataSync = sourceDataSync;
        _equipmentSync = equipmentSync;
        _garlandEnricher = garlandEnricher;
        _cache = cache;
        _logger = logger;
    }

    public async Task<bool> IsFullSyncNeededAsync(CancellationToken ct)
    {
        return !await _db.Items.AnyAsync(ct);
    }

    public async Task<bool> IsVersionChangedAsync(CancellationToken ct)
    {
        var (key, _) = await _xivApiClient.GetCurrentGameVersionAsync(ct);

        var stored = await _db.SyncMetadata
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.EntityType == "GameVersion", ct);

        return stored?.LastPatchVersion != key;
    }

    public async Task RunFullSyncAsync(CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        _logger.LogInformation("Starting full data sync...");

        var (versionKey, versionName) = await _xivApiClient.GetCurrentGameVersionAsync(ct);
        _logger.LogInformation("Syncing against game version {VersionName} ({VersionKey})", versionName, versionKey);

        var (itemCount, itemPriceLookup) = await SyncItemsAsync(ct);
        await UpdateSyncMetadataAsync("Item", itemCount, versionKey, ct);

        var recipeCount = await SyncRecipesAsync(ct);
        await UpdateSyncMetadataAsync("Recipe", recipeCount, versionKey, ct);

        if (_options.SyncRecipeLevelTable)
        {
            var rltCount = await SyncRecipeLevelTableAsync(ct);
            await UpdateSyncMetadataAsync("RecipeLevelTable", rltCount, versionKey, ct);
        }

        if (_options.SyncSources)
        {
            var (sourceCount, gatheringCount, vendorCount) =
                await _sourceDataSync.SyncAllSourcesAsync(itemPriceLookup, ct);
            await UpdateSyncMetadataAsync("ItemSource", sourceCount, versionKey, ct);
            await UpdateSyncMetadataAsync("GatheringInfo", gatheringCount, versionKey, ct);
            await UpdateSyncMetadataAsync("VendorInfo", vendorCount, versionKey, ct);
        }

        var equipCount = 0;
        if (_options.SyncEquipment)
        {
            equipCount = await _equipmentSync.SyncEquipmentAsync(ct);
            await UpdateSyncMetadataAsync("Equipment", equipCount, versionKey, ct);
        }

        if (_options.EnrichFromGarlandTools)
        {
            _logger.LogInformation("Running Garland Tools data enrichment...");
            await _garlandEnricher.EnrichSourceDataAsync(ct);
        }

        await UpdateSyncMetadataAsync("GameVersion", 0, versionKey, ct);

        _logger.LogInformation("Invalidating cached data after sync...");
        await _cache.RemoveByPrefixAsync("materialtree:", ct);
        await _cache.RemoveByPrefixAsync("recipe:", ct);
        await _cache.RemoveByPrefixAsync("item:", ct);
        await _cache.RemoveByPrefixAsync("equipment:", ct);
        await _cache.RemoveByPrefixAsync("gearset:", ct);
        await _cache.RemoveByPrefixAsync("gearslot:", ct);
        await _cache.RemoveByPrefixAsync("gearprofiles:", ct);
        await _cache.RemoveByPrefixAsync("gearsearch:", ct);
        _logger.LogInformation("Cache invalidation complete");

        stopwatch.Stop();
        _logger.LogInformation(
            "Full data sync completed in {Duration}. Items: {ItemCount}, Recipes: {RecipeCount}, Equipment: {EquipCount}",
            stopwatch.Elapsed, itemCount, recipeCount, equipCount);
    }

    private async Task<(int Count, Dictionary<int, int> PriceLookup)> SyncItemsAsync(CancellationToken ct)
    {
        _logger.LogInformation("Starting item sync...");
        int? afterRowId = null;
        var totalSynced = 0;
        var batch = new List<Item>();
        var priceLookup = new Dictionary<int, int>();

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var (rows, lastRowId) = await _xivApiClient.GetSheetAsync<XivApiItemFields>(
                "Item", ItemFields, limit: ItemPageSize, afterRowId: afterRowId, cancellationToken: ct);

            if (rows.Count == 0)
                break;

            foreach (var row in rows)
            {
                if (string.IsNullOrWhiteSpace(row.Fields.Name))
                    continue;

                batch.Add(XivApiDataMapper.ToItem(row.RowId, row.Fields));

                if (row.Fields.PriceMid > 0)
                    priceLookup[row.RowId] = row.Fields.PriceMid;

                if (batch.Count >= _options.BatchSize)
                {
                    await UpsertItemBatchAsync(batch, ct);
                    totalSynced += batch.Count;
                    batch.Clear();

                    if (totalSynced % 1000 < _options.BatchSize)
                        _logger.LogInformation("Synced {RecordCount} {EntityType} so far...", totalSynced, "items");
                }
            }

            afterRowId = lastRowId;
        }

        if (batch.Count > 0)
        {
            await UpsertItemBatchAsync(batch, ct);
            totalSynced += batch.Count;
            batch.Clear();
        }

        _logger.LogInformation("Item sync complete. Total: {RecordCount}", totalSynced);
        return (totalSynced, priceLookup);
    }

    private async Task<int> SyncRecipesAsync(CancellationToken ct)
    {
        _logger.LogInformation("Starting recipe sync...");
        int? afterRowId = null;
        var totalSynced = 0;
        var batch = new List<(Recipe Recipe, List<RecipeIngredient> Ingredients)>();

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var (rows, lastRowId) = await _xivApiClient.GetSheetAsync<XivApiRecipeFields>(
                "Recipe", RecipeFields, limit: RecipePageSize, afterRowId: afterRowId, cancellationToken: ct);

            if (rows.Count == 0)
                break;

            foreach (var row in rows)
            {
                if (row.Fields.ItemResult is null || row.Fields.ItemResult.RowId == 0)
                    continue;

                var recipe = XivApiDataMapper.ToRecipe(row.RowId, row.Fields);
                var ingredients = XivApiDataMapper.ToRecipeIngredients(row.RowId, row.Fields);
                batch.Add((recipe, ingredients));

                if (batch.Count >= _options.BatchSize)
                {
                    await UpsertRecipeBatchAsync(batch, ct);
                    totalSynced += batch.Count;
                    batch.Clear();

                    if (totalSynced % 1000 < _options.BatchSize)
                        _logger.LogInformation("Synced {RecordCount} {EntityType} so far...", totalSynced, "recipes");
                }
            }

            afterRowId = lastRowId;
        }

        if (batch.Count > 0)
        {
            await UpsertRecipeBatchAsync(batch, ct);
            totalSynced += batch.Count;
            batch.Clear();
        }

        _logger.LogInformation("Recipe sync complete. Total: {RecordCount}", totalSynced);
        return totalSynced;
    }

    private async Task UpsertItemBatchAsync(List<Item> items, CancellationToken ct)
    {
        var ids = items.Select(i => i.ItemId).ToHashSet();
        var existing = await _db.Items
            .Where(i => ids.Contains(i.ItemId))
            .ToDictionaryAsync(i => i.ItemId, ct);

        foreach (var item in items)
        {
            if (existing.TryGetValue(item.ItemId, out var tracked))
            {
                tracked.Name = item.Name;
                tracked.IconPath = item.IconPath;
                tracked.ItemLevel = item.ItemLevel;
                tracked.Category = item.Category;
                tracked.CanBeHQ = item.CanBeHQ;
            }
            else
            {
                _db.Items.Add(item);
            }
        }

        await _db.SaveChangesAsync(ct);
        _db.ChangeTracker.Clear();
    }

    private async Task UpsertRecipeBatchAsync(
        List<(Recipe Recipe, List<RecipeIngredient> Ingredients)> recipesWithIngredients,
        CancellationToken ct)
    {
        var recipeIds = recipesWithIngredients.Select(r => r.Recipe.RecipeId).ToHashSet();

        var existingRecipes = await _db.Recipes
            .Where(r => recipeIds.Contains(r.RecipeId))
            .ToDictionaryAsync(r => r.RecipeId, ct);

        if (existingRecipes.Count > 0)
        {
            var existingIngredients = await _db.RecipeIngredients
                .Where(ri => recipeIds.Contains(ri.RecipeId))
                .ToListAsync(ct);
            _db.RecipeIngredients.RemoveRange(existingIngredients);
        }

        foreach (var (recipe, ingredients) in recipesWithIngredients)
        {
            if (existingRecipes.TryGetValue(recipe.RecipeId, out var tracked))
            {
                tracked.ResultItemId = recipe.ResultItemId;
                tracked.CraftType = recipe.CraftType;
                tracked.RecipeLevel = recipe.RecipeLevel;
                tracked.Difficulty = recipe.Difficulty;
                tracked.Quality = recipe.Quality;
                tracked.Durability = recipe.Durability;
                tracked.RequiredCraftsmanship = recipe.RequiredCraftsmanship;
                tracked.RequiredControl = recipe.RequiredControl;
                tracked.Yield = recipe.Yield;
            }
            else
            {
                _db.Recipes.Add(recipe);
            }

            _db.RecipeIngredients.AddRange(ingredients);
        }

        await _db.SaveChangesAsync(ct);
        _db.ChangeTracker.Clear();
    }

    private async Task<int> SyncRecipeLevelTableAsync(CancellationToken ct)
    {
        _logger.LogInformation("Starting RecipeLevelTable sync...");
        int? afterRowId = null;
        var totalSynced = 0;
        var batch = new List<RecipeLevelEntry>();

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var (rows, lastRowId) = await _xivApiClient.GetSheetAsync<XivApiRecipeLevelTableFields>(
                "RecipeLevelTable", RecipeLevelTableFieldList,
                limit: RecipeLevelTablePageSize, afterRowId: afterRowId, cancellationToken: ct);

            if (rows.Count == 0)
                break;

            foreach (var row in rows)
            {
                if (row.RowId == 0 && row.Fields.ClassJobLevel == 0)
                    continue;

                batch.Add(XivApiDataMapper.ToRecipeLevelEntry(row.RowId, row.Fields));

                if (batch.Count >= _options.BatchSize)
                {
                    await UpsertRecipeLevelBatchAsync(batch, ct);
                    totalSynced += batch.Count;
                    batch.Clear();
                }
            }

            afterRowId = lastRowId;
        }

        if (batch.Count > 0)
        {
            await UpsertRecipeLevelBatchAsync(batch, ct);
            totalSynced += batch.Count;
            batch.Clear();
        }

        _logger.LogInformation("RecipeLevelTable sync complete. Total: {RecordCount}", totalSynced);
        return totalSynced;
    }

    private async Task UpsertRecipeLevelBatchAsync(List<RecipeLevelEntry> entries, CancellationToken ct)
    {
        var levels = entries.Select(e => e.RecipeLevel).ToHashSet();
        var existing = await _db.RecipeLevelEntries
            .Where(r => levels.Contains(r.RecipeLevel))
            .ToDictionaryAsync(r => r.RecipeLevel, ct);

        foreach (var entry in entries)
        {
            if (existing.TryGetValue(entry.RecipeLevel, out var tracked))
            {
                tracked.ClassJobLevel = entry.ClassJobLevel;
                tracked.Stars = entry.Stars;
                tracked.Difficulty = entry.Difficulty;
                tracked.Quality = entry.Quality;
                tracked.Durability = entry.Durability;
                tracked.SuggestedCraftsmanship = entry.SuggestedCraftsmanship;
                tracked.SuggestedControl = entry.SuggestedControl;
                tracked.ProgressDivider = entry.ProgressDivider;
                tracked.ProgressModifier = entry.ProgressModifier;
                tracked.QualityDivider = entry.QualityDivider;
                tracked.QualityModifier = entry.QualityModifier;
            }
            else
            {
                _db.RecipeLevelEntries.Add(entry);
            }
        }

        await _db.SaveChangesAsync(ct);
        _db.ChangeTracker.Clear();
    }

    private async Task UpdateSyncMetadataAsync(
        string entityType, int recordCount, string? patchVersion, CancellationToken ct)
    {
        var metadata = await _db.SyncMetadata
            .FirstOrDefaultAsync(s => s.EntityType == entityType, ct);

        if (metadata is null)
        {
            _db.SyncMetadata.Add(new SyncMetadata
            {
                EntityType = entityType,
                LastSyncedAt = DateTimeOffset.UtcNow,
                LastPatchVersion = patchVersion,
                RecordCount = recordCount
            });
        }
        else
        {
            metadata.LastSyncedAt = DateTimeOffset.UtcNow;
            metadata.LastPatchVersion = patchVersion;
            metadata.RecordCount = recordCount;
        }

        await _db.SaveChangesAsync(ct);
    }
}
