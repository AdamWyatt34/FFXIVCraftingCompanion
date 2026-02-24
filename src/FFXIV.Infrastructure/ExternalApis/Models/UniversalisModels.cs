using System.Text.Json.Serialization;

namespace FFXIV.Infrastructure.ExternalApis.Models;

public sealed class UniversalisItemResponse
{
    [JsonPropertyName("itemID")]
    public int ItemId { get; init; }

    [JsonPropertyName("listings")]
    public List<UniversalisListingEntry> Listings { get; init; } = [];

    [JsonPropertyName("averagePrice")]
    public decimal AveragePrice { get; init; }

    [JsonPropertyName("averagePriceNQ")]
    public decimal AveragePriceNQ { get; init; }

    [JsonPropertyName("averagePriceHQ")]
    public decimal AveragePriceHQ { get; init; }

    [JsonPropertyName("minPrice")]
    public decimal MinPrice { get; init; }

    [JsonPropertyName("minPriceNQ")]
    public decimal MinPriceNQ { get; init; }

    [JsonPropertyName("minPriceHQ")]
    public decimal MinPriceHQ { get; init; }

    [JsonPropertyName("listingCount")]
    public int ListingCount { get; init; }

    [JsonPropertyName("lastUploadTime")]
    public long LastUploadTime { get; init; }
}

public sealed class UniversalisBatchResponse
{
    [JsonPropertyName("items")]
    public Dictionary<string, UniversalisItemResponse> Items { get; init; } = new();
}

public sealed class UniversalisListingEntry
{
    [JsonPropertyName("pricePerUnit")]
    public int PricePerUnit { get; init; }

    [JsonPropertyName("quantity")]
    public int Quantity { get; init; }

    [JsonPropertyName("total")]
    public long Total { get; init; }

    [JsonPropertyName("hq")]
    public bool Hq { get; init; }

    [JsonPropertyName("retainerName")]
    public string RetainerName { get; init; } = string.Empty;
}
