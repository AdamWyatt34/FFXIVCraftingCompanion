using System.Net;
using System.Net.Http.Json;
using FFXIV.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace FFXIV.Infrastructure.ExternalApis;

public sealed class GarlandToolsClient : IGarlandToolsClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GarlandToolsClient> _logger;

    public GarlandToolsClient(HttpClient httpClient, ILogger<GarlandToolsClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<GarlandItemData?> GetItemAsync(int itemId, CancellationToken ct = default)
    {
        var url = $"db/doc/item/en/3/{itemId}.json";
        _logger.LogDebug("Fetching Garland Tools item {ItemId}", itemId);

        var response = await _httpClient.GetAsync(url, ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Garland Tools item {ItemId} not found", itemId);
            return null;
        }

        response.EnsureSuccessStatusCode();

        var data = await response.Content.ReadFromJsonAsync<GarlandItemData>(cancellationToken: ct);

        if (data?.Item is not null)
        {
            _logger.LogDebug("Fetched Garland item {ItemId}: {ItemName}", itemId, data.Item.Name);
        }

        return data;
    }
}
