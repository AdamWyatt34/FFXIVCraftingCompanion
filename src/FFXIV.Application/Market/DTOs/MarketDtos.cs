using FFXIV.Domain.Constants;
using FFXIV.Domain.Models;

namespace FFXIV.Application.Market.DTOs;

public sealed record MarketBoardDataDto(
    int ItemId,
    decimal AveragePriceNQ,
    decimal AveragePriceHQ,
    decimal MinPriceNQ,
    decimal MinPriceHQ,
    int CurrentListingCount,
    DateTime LastUploadTime,
    bool IsStale,
    List<MarketListingDto> RecentListings);

public sealed record MarketListingDto(
    int PricePerUnit,
    int Quantity,
    long Total,
    bool IsHQ,
    string RetainerName);

public sealed record CostEstimateDto(
    string WorldOrDc,
    DateTime GeneratedAt,
    List<MaterialCostDto> MaterialCosts,
    long TotalVendorCost,
    long TotalMarketBoardCost,
    long TotalEstimatedCost,
    decimal? FinalItemMarketPrice,
    bool? CraftingIsCheaper,
    long? Savings,
    List<string> Warnings);

public sealed record MaterialCostDto(
    int ItemId,
    string ItemName,
    int Quantity,
    decimal UnitPrice,
    long TotalPrice,
    string PriceSource,
    bool IsStalePrice,
    int ListingCount,
    DateTime? LastUploadTime);

public sealed record WorldGroupDto(
    string Region,
    List<DataCenterDto> DataCenters);

public sealed record DataCenterDto(
    string Name,
    List<string> Worlds);

public static class MarketMapper
{
    public static MarketBoardDataDto ToDto(MarketBoardData data) =>
        new(
            data.ItemId,
            data.AveragePriceNQ,
            data.AveragePriceHQ,
            data.MinPriceNQ,
            data.MinPriceHQ,
            data.CurrentListingCount,
            data.LastUploadTime,
            data.IsStale,
            data.RecentListings.Select(l => new MarketListingDto(
                l.PricePerUnit, l.Quantity, l.Total, l.IsHQ, l.RetainerName)).ToList());

    public static CostEstimateDto ToDto(CostEstimate estimate) =>
        new(
            estimate.WorldOrDc,
            estimate.GeneratedAt,
            estimate.MaterialCosts.Select(m => new MaterialCostDto(
                m.ItemId, m.ItemName, m.Quantity, m.UnitPrice, m.TotalPrice,
                m.PriceSource.ToString(), m.IsStalePrice, m.ListingCount, m.LastUploadTime)).ToList(),
            estimate.TotalVendorCost,
            estimate.TotalMarketBoardCost,
            estimate.TotalEstimatedCost,
            estimate.FinalItemMarketPrice,
            estimate.CraftingIsCheaper,
            estimate.Savings,
            estimate.Warnings);

    public static List<WorldGroupDto> ToWorldGroupDtos(List<WorldGroup> groups) =>
        groups.Select(g => new WorldGroupDto(
            g.Region,
            g.DataCenters.Select(dc => new DataCenterDto(dc.Name, dc.Worlds)).ToList())).ToList();
}
