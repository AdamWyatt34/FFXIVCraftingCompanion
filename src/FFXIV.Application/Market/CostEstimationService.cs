using FFXIV.Domain.Enums;
using FFXIV.Domain.Interfaces;
using FFXIV.Domain.Models;
using Microsoft.Extensions.Logging;

namespace FFXIV.Application.Market;

public sealed class CostEstimationService : ICostEstimationService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(15);

    private readonly IUniversalisClient _universalis;
    private readonly ICacheService _cache;
    private readonly ILogger<CostEstimationService> _logger;
    private readonly TimeProvider _clock;

    public CostEstimationService(
        IUniversalisClient universalis,
        ICacheService cache,
        ILogger<CostEstimationService> logger,
        TimeProvider? clock = null)
    {
        _universalis = universalis;
        _cache = cache;
        _logger = logger;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<CostEstimate> EstimateCostAsync(
        List<AggregatedMaterial> materials,
        int? finalItemId,
        string worldOrDc,
        CostEstimationOptions options,
        int quantity = 1,
        CancellationToken ct = default)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var estimate = new CostEstimate
        {
            WorldOrDc = worldOrDc,
            GeneratedAt = now,
        };

        // Determine which items need market board prices
        var mbItemIds = new HashSet<int>();
        foreach (var mat in materials)
        {
            if (NeedsMarketBoardPrice(mat, options))
                mbItemIds.Add(mat.ItemId);
        }

        if (finalItemId.HasValue && options.IncludeCraftVsBuyAnalysis)
            mbItemIds.Add(finalItemId.Value);

        // Fetch market board data (with caching + partial failure handling)
        var (mbPrices, fetchWarnings) = await GetMarketBoardDataBatchSafeAsync(worldOrDc, mbItemIds, ct);
        estimate.Warnings.AddRange(fetchWarnings);

        // Calculate per-material costs
        foreach (var mat in materials)
        {
            var cost = CalculateMaterialCost(mat, mbPrices, options, now);
            estimate.MaterialCosts.Add(cost);

            switch (cost.PriceSource)
            {
                case PriceSource.Vendor:
                    estimate.TotalVendorCost += cost.TotalPrice;
                    break;
                case PriceSource.MarketBoard:
                    estimate.TotalMarketBoardCost += cost.TotalPrice;
                    break;
            }
        }

        estimate.TotalEstimatedCost = estimate.TotalVendorCost + estimate.TotalMarketBoardCost;

        // Check for materials with unknown prices (tradeable items we couldn't price)
        var unknownPricedMaterials = estimate.MaterialCosts
            .Where(m => m.PriceSource == PriceSource.Unknown)
            .ToList();

        if (unknownPricedMaterials.Count > 0)
        {
            estimate.Warnings.Add(
                $"{unknownPricedMaterials.Count} material(s) have no price data — cost estimate may be incomplete.");
        }

        // Craft vs. buy analysis — only when we have meaningful material cost data
        var pricedMaterialCount = estimate.MaterialCosts.Count(m =>
            m.PriceSource is PriceSource.Vendor or PriceSource.MarketBoard);
        var hasMeaningfulCosts = pricedMaterialCount > 0 && unknownPricedMaterials.Count <= pricedMaterialCount;

        if (finalItemId.HasValue && options.IncludeCraftVsBuyAnalysis &&
            mbPrices.TryGetValue(finalItemId.Value, out var finalItemData))
        {
            var finalPrice = options.UseMinPrice
                ? finalItemData.MinPriceNQ
                : finalItemData.AveragePriceNQ;

            if (finalPrice > 0)
            {
                estimate.FinalItemMarketPrice = finalPrice;

                if (hasMeaningfulCosts)
                {
                    // The materials already cover all the crafts, so they are weighed against buying as many.
                    var buyCost = (long)(finalPrice * quantity);
                    estimate.CraftingIsCheaper = estimate.TotalEstimatedCost < buyCost;
                    estimate.Savings = buyCost - estimate.TotalEstimatedCost;
                }
                else
                {
                    estimate.Warnings.Add(
                        "Craft vs. buy comparison unavailable — too many materials lack price data.");
                }
            }
        }

        return estimate;
    }

    private static bool NeedsMarketBoardPrice(AggregatedMaterial mat, CostEstimationOptions options)
    {
        // Vendor items already have a known price
        if (mat.PrimarySourceType == ItemSourceType.Vendor)
        {
            var vendorSource = mat.Sources.FirstOrDefault(s =>
                s.SourceType == ItemSourceType.Vendor && s.Cost.HasValue);
            if (vendorSource is not null)
                return false;
        }

        // Non-gil sources (scrip, quest, etc.) don't need MB lookup unless we want opportunity cost
        if (mat.PrimarySourceType is ItemSourceType.Scrip or ItemSourceType.Quest
            or ItemSourceType.Dungeon or ItemSourceType.MobDrop
            or ItemSourceType.Desynth or ItemSourceType.Reduction)
        {
            return false;
        }

        // Gathered items: only fetch MB if option is enabled
        if (mat.PrimarySourceType == ItemSourceType.Gathered)
            return options.IncludeGatheredItemPrices;

        // Everything else (Other, Crafted, unknown vendor) needs MB lookup
        return true;
    }

    private MaterialCost CalculateMaterialCost(
        AggregatedMaterial mat,
        Dictionary<int, MarketBoardData> mbPrices,
        CostEstimationOptions options,
        DateTime now)
    {
        // Check vendor price first
        if (mat.PrimarySourceType == ItemSourceType.Vendor)
        {
            var vendorSource = mat.Sources.FirstOrDefault(s =>
                s.SourceType == ItemSourceType.Vendor && s.Cost.HasValue);
            if (vendorSource is not null)
            {
                return new MaterialCost
                {
                    ItemId = mat.ItemId,
                    ItemName = mat.ItemName,
                    Quantity = mat.TotalQuantity,
                    UnitPrice = vendorSource.Cost!.Value,
                    TotalPrice = (long)vendorSource.Cost.Value * mat.TotalQuantity,
                    PriceSource = PriceSource.Vendor,
                };
            }
        }

        // Non-gil sources
        if (mat.PrimarySourceType is ItemSourceType.Scrip or ItemSourceType.Quest
            or ItemSourceType.Dungeon or ItemSourceType.MobDrop
            or ItemSourceType.Desynth or ItemSourceType.Reduction)
        {
            return new MaterialCost
            {
                ItemId = mat.ItemId,
                ItemName = mat.ItemName,
                Quantity = mat.TotalQuantity,
                PriceSource = PriceSource.NonGil,
            };
        }

        // Market board lookup
        if (mbPrices.TryGetValue(mat.ItemId, out var mbData))
        {
            var unitPrice = options.UseMinPrice ? mbData.MinPriceNQ : mbData.AveragePriceNQ;
            return new MaterialCost
            {
                ItemId = mat.ItemId,
                ItemName = mat.ItemName,
                Quantity = mat.TotalQuantity,
                UnitPrice = unitPrice,
                TotalPrice = (long)(unitPrice * mat.TotalQuantity),
                PriceSource = PriceSource.MarketBoard,
                IsStalePrice = (now - mbData.LastUploadTime).TotalHours > 24,
                ListingCount = mbData.CurrentListingCount,
                LastUploadTime = mbData.LastUploadTime,
            };
        }

        // No price data available
        _logger.LogDebug("No price data available for item {ItemId} ({ItemName})", mat.ItemId, mat.ItemName);
        return new MaterialCost
        {
            ItemId = mat.ItemId,
            ItemName = mat.ItemName,
            Quantity = mat.TotalQuantity,
            PriceSource = PriceSource.Unknown,
        };
    }

    private async Task<(Dictionary<int, MarketBoardData> Data, List<string> Warnings)> GetMarketBoardDataBatchSafeAsync(
        string worldOrDc, IEnumerable<int> itemIds, CancellationToken ct)
    {
        var warnings = new List<string>();
        try
        {
            var data = await GetMarketBoardDataBatchAsync(worldOrDc, itemIds, ct);
            return (data, warnings);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Universalis batch fetch failed for {World}", worldOrDc);
            warnings.Add($"Could not fetch some market board prices from Universalis: {ex.Message}");
            return (new Dictionary<int, MarketBoardData>(), warnings);
        }
    }

    internal async Task<Dictionary<int, MarketBoardData>> GetMarketBoardDataBatchAsync(
        string worldOrDc, IEnumerable<int> itemIds, CancellationToken ct)
    {
        var result = new Dictionary<int, MarketBoardData>();
        var uncachedIds = new List<int>();

        foreach (var id in itemIds)
        {
            var cacheKey = $"universalis:{worldOrDc}:{id}";
            var cached = await _cache.GetAsync<MarketBoardData>(cacheKey, ct);
            if (cached is not null)
            {
                result[id] = cached;
            }
            else
            {
                uncachedIds.Add(id);
            }
        }

        if (uncachedIds.Count == 0)
            return result;

        _logger.LogDebug("Fetching {Count} uncached items from Universalis for {World}",
            uncachedIds.Count, worldOrDc);

        var fetched = await _universalis.GetItemPricesBatchAsync(worldOrDc, uncachedIds, ct);

        foreach (var (id, data) in fetched)
        {
            result[id] = data;
            var cacheKey = $"universalis:{worldOrDc}:{id}";
            await _cache.SetAsync(cacheKey, data, CacheTtl, ct);
        }

        return result;
    }
}
