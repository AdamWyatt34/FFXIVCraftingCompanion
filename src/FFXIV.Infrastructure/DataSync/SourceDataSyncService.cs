using System.Diagnostics;
using System.Text.Json;
using FFXIV.Domain.Entities;
using FFXIV.Domain.Enums;
using FFXIV.Domain.Interfaces;
using FFXIV.Infrastructure.ExternalApis;
using FFXIV.Infrastructure.ExternalApis.Models;
using FFXIV.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FFXIV.Infrastructure.DataSync;

/// <summary>
/// Syncs item source/acquisition data from XIVAPI v2 sheets.
/// Called by <see cref="DataSyncOrchestrator"/> after Items and Recipes have been synced.
/// </summary>
public sealed class SourceDataSyncService
{
    private readonly AppDbContext _db;
    private readonly IXivApiClient _xivApiClient;
    private readonly DataSyncOptions _options;
    private readonly ILogger<SourceDataSyncService> _logger;

    private const int PageSize = 500;

    // Crystal/Shard/Cluster item IDs (always-available gathering materials)
    private const int CrystalMinId = 2;
    private const int CrystalMaxId = 19;

    public SourceDataSyncService(
        AppDbContext db,
        IXivApiClient xivApiClient,
        IOptions<DataSyncOptions> options,
        ILogger<SourceDataSyncService> logger)
    {
        _db = db;
        _xivApiClient = xivApiClient;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Syncs all item source data. Returns counts of records created/updated.
    /// </summary>
    public async Task<(int Sources, int Gathering, int Vendors)> SyncAllSourcesAsync(
        Dictionary<int, int> itemPriceLookup,
        CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        _logger.LogInformation("Starting item source data sync...");

        var gatheringCount = 0;
        var vendorCount = 0;
        var sourceCount = 0;

        // Load known item IDs once — used to filter out FK violations from XIVAPI data
        // referencing items that weren't synced to our Items table
        var knownItemIds = new HashSet<int>(
            await _db.Items.Select(i => i.ItemId).ToListAsync(ct));
        _logger.LogDebug("Loaded {Count} known item IDs for FK validation", knownItemIds.Count);

        if (_options.SyncGathering)
        {
            gatheringCount = await SyncGatheringDataAsync(knownItemIds, ct);
        }

        if (_options.SyncVendors)
        {
            vendorCount = await SyncVendorDataAsync(itemPriceLookup, knownItemIds, ct);
        }

        if (_options.SyncScrips)
        {
            sourceCount += await SyncScripDataAsync(ct);
        }

        sourceCount += await SyncCraftedSourcesAsync(ct);
        sourceCount += await SyncCrystalSourcesAsync(ct);

        stopwatch.Stop();
        _logger.LogInformation(
            "Item source sync completed in {Duration}. Sources: {SourceCount}, Gathering: {GatheringCount}, Vendors: {VendorCount}",
            stopwatch.Elapsed, sourceCount, gatheringCount, vendorCount);

        return (sourceCount, gatheringCount, vendorCount);
    }

    #region Gathering Data Sync

    private async Task<int> SyncGatheringDataAsync(HashSet<int> knownItemIds, CancellationToken ct)
    {
        _logger.LogInformation("Starting gathering data sync...");

        // Step 1: Load GatheringItem sheet → maps gatheringItemRowId to (ItemId, Level, IsHidden)
        var gatheringItems = await LoadGatheringItemsAsync(ct);
        _logger.LogInformation("Loaded {Count} gathering items from XIVAPI", gatheringItems.Count);

        // Step 2: Load GatheringPointBase sheet → maps baseRowId to (GatheringTypeRowId, Level, GatheringItemRowIds)
        var pointBases = await LoadGatheringPointBasesAsync(ct);
        _logger.LogInformation("Loaded {Count} gathering point bases from XIVAPI", pointBases.Count);

        // Step 3: Load GatheringPoint sheet → maps pointRowId to (BaseRowId, TerritoryTypeRowId)
        var points = await LoadGatheringPointsAsync(ct);
        _logger.LogInformation("Loaded {Count} gathering points from XIVAPI", points.Count);

        // Step 4: Load TerritoryType sheet → maps territoryRowId to zone name
        var territories = await LoadTerritoryNamesAsync(ct);
        _logger.LogInformation("Loaded {Count} territory names from XIVAPI", territories.Count);

        // Step 5: Load GatheringPointTransient sheet → identify timed nodes
        var transients = await LoadGatheringPointTransientsAsync(ct);
        _logger.LogInformation("Loaded {Count} gathering point transients from XIVAPI", transients.Count);

        // Step 6: In-memory join to build GatheringInfo entities
        var gatheringInfos = new List<GatheringInfo>();
        var gatheringItemSources = new List<ItemSource>();
        var processedItemIds = new HashSet<int>();
        var skippedCount = 0;

        foreach (var (pointRowId, point) in points)
        {
            if (!pointBases.TryGetValue(point.BaseRowId, out var pointBase))
                continue;

            var zoneName = territories.GetValueOrDefault(point.TerritoryTypeRowId, "Unknown");
            var gatheringType = XivApiDataMapper.MapGatheringType(pointBase.GatheringTypeRowId);
            var classJob = XivApiDataMapper.GatheringTypeToClassJob(gatheringType);

            // Check if this is a timed node
            var isTimed = false;
            string? spawnTimes = null;
            if (transients.TryGetValue(pointRowId, out var transient))
            {
                if (transient.StartTime != 0 || transient.EndTime != 0)
                {
                    isTimed = true;
                    spawnTimes = JsonSerializer.Serialize(new[] { $"{transient.StartTime}:00", $"{transient.EndTime}:00" });
                }
            }

            foreach (var gatheringItemRowId in pointBase.GatheringItemRowIds)
            {
                if (!gatheringItems.TryGetValue(gatheringItemRowId, out var gatheringItem))
                    continue;

                if (gatheringItem.ItemId == 0)
                    continue;

                if (!knownItemIds.Contains(gatheringItem.ItemId))
                {
                    skippedCount++;
                    continue;
                }

                gatheringInfos.Add(new GatheringInfo
                {
                    ItemId = gatheringItem.ItemId,
                    GatheringType = gatheringType,
                    ClassJob = classJob,
                    Level = pointBase.Level > 0 ? pointBase.Level : gatheringItem.Level,
                    Stars = 0,
                    ZoneName = zoneName,
                    IsTimed = isTimed,
                    SpawnTimes = spawnTimes,
                    IsHidden = gatheringItem.IsHidden
                });

                processedItemIds.Add(gatheringItem.ItemId);
            }
        }

        // Build ItemSource entries for gathered items
        foreach (var itemId in processedItemIds)
        {
            gatheringItemSources.Add(new ItemSource
            {
                ItemId = itemId,
                SourceType = ItemSourceType.Gathered,
                SourceDetails = "{}"
            });
        }

        if (skippedCount > 0)
            _logger.LogWarning("Skipped {Count} gathering entries referencing items not in the Items table", skippedCount);

        // Batch upsert
        _logger.LogInformation("Upserting {Count} gathering info records for {ItemCount} items...",
            gatheringInfos.Count, processedItemIds.Count);

        await ReplaceGatheringInfoBatchAsync(gatheringInfos, ct);
        await UpsertItemSourceBatchAsync(gatheringItemSources, ct);

        _logger.LogInformation("Gathering data sync complete. Records: {Count}", gatheringInfos.Count);
        return gatheringInfos.Count;
    }

    private async Task<Dictionary<int, (int ItemId, int Level, bool IsHidden)>> LoadGatheringItemsAsync(CancellationToken ct)
    {
        var result = new Dictionary<int, (int ItemId, int Level, bool IsHidden)>();
        int? afterRowId = null;

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var (rows, lastRowId) = await _xivApiClient.GetSheetAsync<XivApiGatheringItemFields>(
                "GatheringItem", "Item,GatheringItemLevel,IsHidden",
                limit: PageSize, afterRowId: afterRowId, cancellationToken: ct);

            if (rows.Count == 0) break;

            foreach (var row in rows)
            {
                if (row.Fields.Item is null || row.Fields.Item.RowId == 0)
                    continue;

                result[row.RowId] = (
                    ItemId: row.Fields.Item.RowId,
                    Level: row.Fields.GatheringItemLevel?.RowId ?? 0,
                    IsHidden: row.Fields.IsHidden
                );
            }

            afterRowId = lastRowId;
        }

        return result;
    }

    private async Task<Dictionary<int, (int GatheringTypeRowId, int Level, List<int> GatheringItemRowIds)>> LoadGatheringPointBasesAsync(CancellationToken ct)
    {
        var result = new Dictionary<int, (int GatheringTypeRowId, int Level, List<int> GatheringItemRowIds)>();
        int? afterRowId = null;

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var (rows, lastRowId) = await _xivApiClient.GetSheetAsync<XivApiGatheringPointBaseFields>(
                "GatheringPointBase", "GatheringType@as(raw),GatheringLevel,Item@as(raw)",
                limit: PageSize, afterRowId: afterRowId, cancellationToken: ct);

            if (rows.Count == 0) break;

            foreach (var row in rows)
            {
                var gatheringItemRowIds = new List<int>();
                if (row.Fields.Item is not null)
                {
                    foreach (var itemRowId in row.Fields.Item)
                    {
                        if (itemRowId != 0)
                            gatheringItemRowIds.Add(itemRowId);
                    }
                }

                if (gatheringItemRowIds.Count > 0)
                {
                    result[row.RowId] = (
                        GatheringTypeRowId: row.Fields.GatheringTypeRowId,
                        Level: row.Fields.GatheringLevel,
                        GatheringItemRowIds: gatheringItemRowIds
                    );
                }
            }

            afterRowId = lastRowId;
        }

        return result;
    }

    private async Task<Dictionary<int, (int BaseRowId, int TerritoryTypeRowId)>> LoadGatheringPointsAsync(CancellationToken ct)
    {
        var result = new Dictionary<int, (int BaseRowId, int TerritoryTypeRowId)>();
        int? afterRowId = null;

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var (rows, lastRowId) = await _xivApiClient.GetSheetAsync<XivApiGatheringPointFields>(
                "GatheringPoint", "GatheringPointBase,TerritoryType",
                limit: PageSize, afterRowId: afterRowId, cancellationToken: ct);

            if (rows.Count == 0) break;

            foreach (var row in rows)
            {
                if (row.Fields.GatheringPointBase is null || row.Fields.GatheringPointBase.RowId == 0)
                    continue;

                result[row.RowId] = (
                    BaseRowId: row.Fields.GatheringPointBase.RowId,
                    TerritoryTypeRowId: row.Fields.TerritoryType?.RowId ?? 0
                );
            }

            afterRowId = lastRowId;
        }

        return result;
    }

    private async Task<Dictionary<int, string>> LoadTerritoryNamesAsync(CancellationToken ct)
    {
        var result = new Dictionary<int, string>();
        int? afterRowId = null;

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var (rows, lastRowId) = await _xivApiClient.GetSheetAsync<XivApiTerritoryTypeFields>(
                "TerritoryType", "PlaceName",
                limit: PageSize, afterRowId: afterRowId, cancellationToken: ct);

            if (rows.Count == 0) break;

            foreach (var row in rows)
            {
                var name = row.Fields.PlaceName?.Fields?.Name;
                if (!string.IsNullOrWhiteSpace(name))
                {
                    result[row.RowId] = name;
                }
            }

            afterRowId = lastRowId;
        }

        return result;
    }

    private async Task<Dictionary<int, (int StartTime, int EndTime)>> LoadGatheringPointTransientsAsync(CancellationToken ct)
    {
        var result = new Dictionary<int, (int StartTime, int EndTime)>();
        int? afterRowId = null;

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var (rows, lastRowId) = await _xivApiClient.GetSheetAsync<XivApiGatheringPointTransientFields>(
                "GatheringPointTransient", "EphemeralStartTime,EphemeralEndTime",
                limit: PageSize, afterRowId: afterRowId, cancellationToken: ct);

            if (rows.Count == 0) break;

            foreach (var row in rows)
            {
                if (row.Fields.EphemeralStartTime != 0 || row.Fields.EphemeralEndTime != 0)
                {
                    result[row.RowId] = (
                        StartTime: row.Fields.EphemeralStartTime,
                        EndTime: row.Fields.EphemeralEndTime
                    );
                }
            }

            afterRowId = lastRowId;
        }

        return result;
    }

    #endregion

    #region Vendor Data Sync

    private async Task<int> SyncVendorDataAsync(Dictionary<int, int> itemPriceLookup, HashSet<int> knownItemIds, CancellationToken ct)
    {
        _logger.LogInformation("Starting vendor data sync...");

        // Step 1: Load GilShopItem sub-row sheet → list of (shopRowId, itemId)
        var gilShopItems = await LoadGilShopItemsAsync(ct);
        _logger.LogInformation("Loaded {Count} gil shop items from XIVAPI", gilShopItems.Count);

        // Step 2: Load GilShop sheet → shopRowId to name
        var gilShopNames = await LoadGilShopNamesAsync(ct);
        _logger.LogInformation("Loaded {Count} gil shops from XIVAPI", gilShopNames.Count);

        // Step 3: Build VendorInfo and ItemSource records
        // GilShopItem.row_id == GilShop.row_id, so we match shops by row_id directly
        var vendorInfos = new List<VendorInfo>();
        var vendorItemSources = new List<ItemSource>();
        var processedItemIds = new HashSet<int>();

        foreach (var (shopRowId, itemId) in gilShopItems)
        {
            if (itemId == 0) continue;
            if (!knownItemIds.Contains(itemId)) continue;

            var shopName = gilShopNames.GetValueOrDefault(shopRowId, "Gil Vendor");
            var cost = itemPriceLookup.GetValueOrDefault(itemId, 0);

            // Skip items with zero cost (likely not actually purchasable)
            if (cost == 0) continue;

            vendorInfos.Add(new VendorInfo
            {
                ItemId = itemId,
                NpcName = shopName,
                ZoneName = "Unknown",
                Cost = cost,
                CurrencyType = "Gil"
            });

            processedItemIds.Add(itemId);
        }

        // Build ItemSource entries for vendor items
        foreach (var itemId in processedItemIds)
        {
            vendorItemSources.Add(new ItemSource
            {
                ItemId = itemId,
                SourceType = ItemSourceType.Vendor,
                SourceDetails = "{}"
            });
        }

        // Batch upsert
        _logger.LogInformation("Upserting {Count} vendor info records for {ItemCount} items...",
            vendorInfos.Count, processedItemIds.Count);

        await ReplaceVendorInfoBatchAsync(vendorInfos, ct);
        await UpsertItemSourceBatchAsync(vendorItemSources, ct);

        _logger.LogInformation("Vendor data sync complete. Records: {Count}", vendorInfos.Count);
        return vendorInfos.Count;
    }

    /// <summary>
    /// Loads GilShopItem — a sub-row sheet where row_id is the parent GilShop ID
    /// and each sub-row represents one item sold in that shop.
    /// Sub-row sheets require special pagination: when lastRowId == afterRowId,
    /// we're on the final parent row's sub-rows and must break.
    /// </summary>
    private async Task<List<(int ShopRowId, int ItemId)>> LoadGilShopItemsAsync(CancellationToken ct)
    {
        var result = new List<(int ShopRowId, int ItemId)>();
        int? afterRowId = null;

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var (rows, lastRowId) = await _xivApiClient.GetSheetAsync<XivApiGilShopItemFields>(
                "GilShopItem", "Item@as(raw)",
                limit: PageSize, afterRowId: afterRowId, cancellationToken: ct);

            if (rows.Count == 0) break;

            foreach (var row in rows)
            {
                if (row.Fields.ItemRowId != 0)
                {
                    result.Add((ShopRowId: row.RowId, ItemId: row.Fields.ItemRowId));
                }
            }

            // Sub-row sheet pagination: when lastRowId hasn't advanced,
            // we're stuck on the final parent row's sub-rows. Break to avoid infinite loop.
            if (lastRowId == afterRowId)
                break;

            afterRowId = lastRowId;
        }

        return result;
    }

    private async Task<Dictionary<int, string>> LoadGilShopNamesAsync(CancellationToken ct)
    {
        var result = new Dictionary<int, string>();
        int? afterRowId = null;

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var (rows, lastRowId) = await _xivApiClient.GetSheetAsync<XivApiGilShopFields>(
                "GilShop", "Name",
                limit: PageSize, afterRowId: afterRowId, cancellationToken: ct);

            if (rows.Count == 0) break;

            foreach (var row in rows)
            {
                if (!string.IsNullOrWhiteSpace(row.Fields.Name))
                {
                    result[row.RowId] = row.Fields.Name;
                }
            }

            afterRowId = lastRowId;
        }

        return result;
    }

    #endregion

    #region Scrip Data Sync

    private async Task<int> SyncScripDataAsync(CancellationToken ct)
    {
        _logger.LogInformation("Starting scrip exchange data sync (simplified)...");

        // SpecialShop has a complex nested structure. For v1, we do a best-effort
        // scan and mark items that appear with SourceType=Scrip.
        // Full SpecialShop parsing is deferred to a later sprint.

        // For now, look for common scrip exchange items by checking existing items
        // that have known scrip-related ItemUICategory values.
        // This is a simplified heuristic approach.

        var scripSources = new List<ItemSource>();

        // Query items that are likely scrip-purchasable based on category
        // (Items categorized as Material that are commonly exchanged for scrips
        // will be identified once full SpecialShop parsing is implemented)

        _logger.LogInformation("Scrip exchange sync complete (simplified). Marked {Count} items", scripSources.Count);
        return scripSources.Count;
    }

    #endregion

    #region Crafted Sources (Derived from DB)

    private async Task<int> SyncCraftedSourcesAsync(CancellationToken ct)
    {
        _logger.LogInformation("Syncing crafted item sources from existing recipe data...");

        var recipeResults = await _db.Recipes
            .AsNoTracking()
            .Select(r => new { r.RecipeId, r.ResultItemId })
            .ToListAsync(ct);

        // Group by ResultItemId since multiple recipes can produce the same item
        var craftedSources = recipeResults
            .GroupBy(r => r.ResultItemId)
            .Select(g => new ItemSource
            {
                ItemId = g.Key,
                SourceType = ItemSourceType.Crafted,
                SourceDetails = JsonSerializer.Serialize(new
                {
                    recipeIds = g.Select(r => r.RecipeId).ToArray()
                })
            })
            .ToList();

        await UpsertItemSourceBatchAsync(craftedSources, ct);

        _logger.LogInformation("Crafted source sync complete. Marked {Count} items as craftable", craftedSources.Count);
        return craftedSources.Count;
    }

    #endregion

    #region Crystal Sources

    private async Task<int> SyncCrystalSourcesAsync(CancellationToken ct)
    {
        _logger.LogInformation("Syncing crystal/shard item sources...");

        // Items 2-19 are crystals, shards, and clusters — always-available terminal nodes
        var crystalIds = await _db.Items
            .AsNoTracking()
            .Where(i => i.ItemId >= CrystalMinId && i.ItemId <= CrystalMaxId)
            .Select(i => i.ItemId)
            .ToListAsync(ct);

        var crystalSources = crystalIds
            .Select(id => new ItemSource
            {
                ItemId = id,
                SourceType = ItemSourceType.Gathered,
                SourceDetails = JsonSerializer.Serialize(new { isCrystal = true })
            })
            .ToList();

        await UpsertItemSourceBatchAsync(crystalSources, ct);

        _logger.LogInformation("Crystal source sync complete. Marked {Count} crystal items", crystalSources.Count);
        return crystalSources.Count;
    }

    #endregion

    #region Upsert Helpers

    private async Task UpsertItemSourceBatchAsync(List<ItemSource> sources, CancellationToken ct)
    {
        if (sources.Count == 0) return;

        // Process in batches to avoid loading too many tracked entities
        foreach (var batch in sources.Chunk(_options.BatchSize))
        {
            var itemIds = batch.Select(s => s.ItemId).Distinct().ToHashSet();
            var sourceTypes = batch.Select(s => s.SourceType).Distinct().ToHashSet();

            var existing = await _db.ItemSources
                .Where(s => itemIds.Contains(s.ItemId) && sourceTypes.Contains(s.SourceType))
                .ToDictionaryAsync(s => (s.ItemId, s.SourceType), ct);

            foreach (var source in batch)
            {
                var key = (source.ItemId, source.SourceType);
                if (existing.TryGetValue(key, out var tracked))
                {
                    tracked.SourceDetails = source.SourceDetails;
                }
                else
                {
                    _db.ItemSources.Add(source);
                }
            }

            await _db.SaveChangesAsync(ct);
            _db.ChangeTracker.Clear();
        }
    }

    private async Task ReplaceGatheringInfoBatchAsync(List<GatheringInfo> infos, CancellationToken ct)
    {
        if (infos.Count == 0) return;

        // Process in batches grouped by ItemId
        var byItem = infos.GroupBy(g => g.ItemId).ToList();

        foreach (var batch in byItem.Chunk(_options.BatchSize))
        {
            var itemIds = batch.Select(g => g.Key).ToHashSet();

            var existing = await _db.GatheringInfos
                .Where(g => itemIds.Contains(g.ItemId))
                .ToListAsync(ct);

            _db.GatheringInfos.RemoveRange(existing);

            foreach (var group in batch)
            {
                _db.GatheringInfos.AddRange(group);
            }

            await _db.SaveChangesAsync(ct);
            _db.ChangeTracker.Clear();
        }
    }

    private async Task ReplaceVendorInfoBatchAsync(List<VendorInfo> infos, CancellationToken ct)
    {
        if (infos.Count == 0) return;

        // Process in batches grouped by ItemId
        var byItem = infos.GroupBy(v => v.ItemId).ToList();

        foreach (var batch in byItem.Chunk(_options.BatchSize))
        {
            var itemIds = batch.Select(g => g.Key).ToHashSet();

            var existing = await _db.VendorInfos
                .Where(v => itemIds.Contains(v.ItemId))
                .ToListAsync(ct);

            _db.VendorInfos.RemoveRange(existing);

            foreach (var group in batch)
            {
                _db.VendorInfos.AddRange(group);
            }

            await _db.SaveChangesAsync(ct);
            _db.ChangeTracker.Clear();
        }
    }

    #endregion
}
