namespace FFXIV.Domain.Models;

public class MarketBoardData
{
    public int ItemId { get; set; }
    public decimal AveragePriceNQ { get; set; }
    public decimal AveragePriceHQ { get; set; }
    public decimal MinPriceNQ { get; set; }
    public decimal MinPriceHQ { get; set; }
    public int CurrentListingCount { get; set; }
    public DateTime LastUploadTime { get; set; }
    public List<MarketListing> RecentListings { get; set; } = [];

    public bool IsStale => (DateTime.UtcNow - LastUploadTime).TotalHours > 24;
}

public class MarketListing
{
    public int PricePerUnit { get; set; }
    public int Quantity { get; set; }
    public long Total { get; set; }
    public bool IsHQ { get; set; }
    public string RetainerName { get; set; } = string.Empty;
}

public class CostEstimate
{
    public string WorldOrDc { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public List<MaterialCost> MaterialCosts { get; set; } = [];
    public long TotalVendorCost { get; set; }
    public long TotalMarketBoardCost { get; set; }
    public long TotalEstimatedCost { get; set; }
    public decimal? FinalItemMarketPrice { get; set; }
    public bool? CraftingIsCheaper { get; set; }
    public long? Savings { get; set; }
    public List<string> Warnings { get; set; } = [];
}

public class MaterialCost
{
    public int ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public long TotalPrice { get; set; }
    public PriceSource PriceSource { get; set; }
    public bool IsStalePrice { get; set; }
    public int ListingCount { get; set; }
    public DateTime? LastUploadTime { get; set; }
}

public enum PriceSource
{
    Vendor,
    MarketBoard,
    NonGil,
    Unknown
}

public class CostEstimationOptions
{
    public bool UseMinPrice { get; set; }
    public bool IncludeCraftVsBuyAnalysis { get; set; } = true;
    public bool IncludeGatheredItemPrices { get; set; } = true;
}
