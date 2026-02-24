using System.Net;
using System.Net.Http.Json;
using FFXIV.Domain.Interfaces;
using FFXIV.Domain.Models;
using FFXIV.Infrastructure.ExternalApis.Models;
using Microsoft.Extensions.Logging;

namespace FFXIV.Infrastructure.ExternalApis;

public sealed class UniversalisClient : IUniversalisClient
{
    private const int MaxBatchSize = 100;
    private static readonly TimeSpan ChunkDelay = TimeSpan.FromMilliseconds(250);

    private readonly HttpClient _httpClient;
    private readonly ILogger<UniversalisClient> _logger;

    public UniversalisClient(HttpClient httpClient, ILogger<UniversalisClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<MarketBoardData?> GetItemPricesAsync(
        string worldOrDc, int itemId, CancellationToken ct = default)
    {
        var url = $"{worldOrDc}/{itemId}";
        _logger.LogDebug("Fetching Universalis prices for item {ItemId} on {World}", itemId, worldOrDc);

        var response = await _httpClient.GetAsync(url, ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogDebug("Item {ItemId} not found on Universalis (not marketable?)", itemId);
            return null;
        }

        response.EnsureSuccessStatusCode();

        var data = await response.Content.ReadFromJsonAsync<UniversalisItemResponse>(cancellationToken: ct);
        return data is null ? null : MapToMarketBoardData(data);
    }

    public async Task<Dictionary<int, MarketBoardData>> GetItemPricesBatchAsync(
        string worldOrDc, IEnumerable<int> itemIds, CancellationToken ct = default)
    {
        var result = new Dictionary<int, MarketBoardData>();
        var idList = itemIds.Distinct().ToList();

        if (idList.Count == 0)
            return result;

        var chunks = idList.Chunk(MaxBatchSize).ToList();
        _logger.LogDebug("Fetching Universalis batch prices: {Count} items in {Chunks} chunk(s) on {World}",
            idList.Count, chunks.Count, worldOrDc);

        for (var i = 0; i < chunks.Count; i++)
        {
            if (i > 0)
                await Task.Delay(ChunkDelay, ct);

            var chunk = chunks[i];
            var ids = string.Join(',', chunk);
            var url = $"{worldOrDc}/{ids}";

            var response = await _httpClient.GetAsync(url, ct);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogDebug("Batch request returned 404 for chunk {Index} on {World}", i, worldOrDc);
                continue;
            }

            response.EnsureSuccessStatusCode();

            if (chunk.Length == 1)
            {
                // Single item returns UniversalisItemResponse directly, not wrapped in items dict
                var single = await response.Content.ReadFromJsonAsync<UniversalisItemResponse>(cancellationToken: ct);
                if (single is not null)
                {
                    result[single.ItemId] = MapToMarketBoardData(single);
                }
            }
            else
            {
                var batch = await response.Content.ReadFromJsonAsync<UniversalisBatchResponse>(cancellationToken: ct);
                if (batch?.Items is not null)
                {
                    foreach (var (key, item) in batch.Items)
                    {
                        if (int.TryParse(key, out var parsedId))
                        {
                            result[parsedId] = MapToMarketBoardData(item);
                        }
                    }
                }
            }
        }

        _logger.LogDebug("Fetched prices for {Count}/{Total} items from Universalis", result.Count, idList.Count);
        return result;
    }

    private static MarketBoardData MapToMarketBoardData(UniversalisItemResponse response)
    {
        return new MarketBoardData
        {
            ItemId = response.ItemId,
            AveragePriceNQ = response.AveragePriceNQ,
            AveragePriceHQ = response.AveragePriceHQ,
            MinPriceNQ = response.MinPriceNQ,
            MinPriceHQ = response.MinPriceHQ,
            CurrentListingCount = response.ListingCount,
            LastUploadTime = DateTime.UnixEpoch.AddMilliseconds(response.LastUploadTime),
            RecentListings = response.Listings.Take(10).Select(l => new MarketListing
            {
                PricePerUnit = l.PricePerUnit,
                Quantity = l.Quantity,
                Total = l.Total,
                IsHQ = l.Hq,
                RetainerName = l.RetainerName,
            }).ToList(),
        };
    }
}
