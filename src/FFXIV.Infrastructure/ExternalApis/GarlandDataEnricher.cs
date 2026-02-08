using FFXIV.Domain.Entities;
using FFXIV.Domain.Enums;
using FFXIV.Domain.Interfaces;
using FFXIV.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FFXIV.Infrastructure.ExternalApis;

public sealed class GarlandDataEnricher
{
    private readonly IGarlandToolsClient _garlandClient;
    private readonly AppDbContext _db;
    private readonly ILogger<GarlandDataEnricher> _logger;

    public GarlandDataEnricher(
        IGarlandToolsClient garlandClient,
        AppDbContext db,
        ILogger<GarlandDataEnricher> logger)
    {
        _garlandClient = garlandClient;
        _db = db;
        _logger = logger;
    }

    public async Task EnrichSourceDataAsync(CancellationToken ct)
    {
        var itemsWithoutSources = await _db.Items
            .Where(i => !i.Sources.Any())
            .Select(i => i.ItemId)
            .ToListAsync(ct);

        _logger.LogInformation("Found {Count} items without source data to enrich from Garland Tools",
            itemsWithoutSources.Count);

        var enrichedCount = 0;

        foreach (var itemId in itemsWithoutSources)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var garlandData = await _garlandClient.GetItemAsync(itemId, ct);
                if (garlandData?.Item is null)
                    continue;

                var sourcesAdded = false;

                if (garlandData.Item.Vendors is { Count: > 0 })
                {
                    var existingVendor = await _db.ItemSources
                        .AnyAsync(s => s.ItemId == itemId && s.SourceType == ItemSourceType.Vendor, ct);

                    if (!existingVendor)
                    {
                        _db.ItemSources.Add(new ItemSource
                        {
                            ItemId = itemId,
                            SourceType = ItemSourceType.Vendor,
                            SourceDetails = "{}"
                        });
                        sourcesAdded = true;
                    }
                }

                if (garlandData.Item.Nodes is { Count: > 0 })
                {
                    var existingGathered = await _db.ItemSources
                        .AnyAsync(s => s.ItemId == itemId && s.SourceType == ItemSourceType.Gathered, ct);

                    if (!existingGathered)
                    {
                        _db.ItemSources.Add(new ItemSource
                        {
                            ItemId = itemId,
                            SourceType = ItemSourceType.Gathered,
                            SourceDetails = "{}"
                        });
                        sourcesAdded = true;
                    }
                }

                if (sourcesAdded)
                {
                    await _db.SaveChangesAsync(ct);
                    enrichedCount++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to enrich item {ItemId} from Garland Tools", itemId);
            }
        }

        _logger.LogInformation("Enriched {Count} items from Garland Tools", enrichedCount);
    }
}
