using FFXIV.Application.Market;
using FFXIV.Domain.Enums;
using FFXIV.Domain.Interfaces;
using FFXIV.Domain.Models;
using Microsoft.Extensions.Logging;

namespace FFXIV.Tests.Market;

public class CostEstimationServiceTests
{
    private readonly IUniversalisClient _universalis = Substitute.For<IUniversalisClient>();
    private readonly ICacheService _cache = Substitute.For<ICacheService>();
    private readonly ILogger<CostEstimationService> _logger = Substitute.For<ILogger<CostEstimationService>>();
    private readonly CostEstimationService _service;

    public CostEstimationServiceTests()
    {
        // Cache always misses by default
        _cache.GetAsync<MarketBoardData>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((MarketBoardData?)null);

        _service = new CostEstimationService(_universalis, _cache, _logger);
    }

    [Fact]
    public async Task VendorItems_UseVendorPrice()
    {
        // Arrange
        var materials = new List<AggregatedMaterial>
        {
            CreateMaterial(100, "Vendor Item", 5, ItemSourceType.Vendor,
                vendorCost: 200)
        };
        var options = new CostEstimationOptions();

        // Act
        var result = await _service.EstimateCostAsync(materials, null, "Adamantoise", options);

        // Assert
        result.MaterialCosts.Should().HaveCount(1);
        result.MaterialCosts[0].PriceSource.Should().Be(PriceSource.Vendor);
        result.MaterialCosts[0].UnitPrice.Should().Be(200);
        result.MaterialCosts[0].TotalPrice.Should().Be(1000); // 200 * 5
        result.TotalVendorCost.Should().Be(1000);
        result.TotalMarketBoardCost.Should().Be(0);
    }

    [Fact]
    public async Task MarketBoardItems_UseAveragePriceByDefault()
    {
        // Arrange
        var materials = new List<AggregatedMaterial>
        {
            CreateMaterial(200, "MB Item", 3, ItemSourceType.Other)
        };
        var mbData = CreateMarketBoardData(200, avgNq: 500, minNq: 300);
        _universalis.GetItemPricesBatchAsync("Adamantoise", Arg.Any<IEnumerable<int>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<int, MarketBoardData> { { 200, mbData } });

        var options = new CostEstimationOptions { UseMinPrice = false };

        // Act
        var result = await _service.EstimateCostAsync(materials, null, "Adamantoise", options);

        // Assert
        result.MaterialCosts[0].PriceSource.Should().Be(PriceSource.MarketBoard);
        result.MaterialCosts[0].UnitPrice.Should().Be(500);
        result.MaterialCosts[0].TotalPrice.Should().Be(1500);
        result.TotalMarketBoardCost.Should().Be(1500);
    }

    [Fact]
    public async Task MarketBoardItems_UseMinPriceWhenOptionSet()
    {
        // Arrange
        var materials = new List<AggregatedMaterial>
        {
            CreateMaterial(200, "MB Item", 3, ItemSourceType.Other)
        };
        var mbData = CreateMarketBoardData(200, avgNq: 500, minNq: 300);
        _universalis.GetItemPricesBatchAsync("Adamantoise", Arg.Any<IEnumerable<int>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<int, MarketBoardData> { { 200, mbData } });

        var options = new CostEstimationOptions { UseMinPrice = true };

        // Act
        var result = await _service.EstimateCostAsync(materials, null, "Adamantoise", options);

        // Assert
        result.MaterialCosts[0].UnitPrice.Should().Be(300);
        result.MaterialCosts[0].TotalPrice.Should().Be(900);
    }

    [Fact]
    public async Task GatheredItems_NoMBLookupByDefault()
    {
        // Arrange
        var materials = new List<AggregatedMaterial>
        {
            CreateMaterial(300, "Ore", 10, ItemSourceType.Gathered)
        };
        var options = new CostEstimationOptions { IncludeGatheredItemPrices = false };

        // Act
        var result = await _service.EstimateCostAsync(materials, null, "Adamantoise", options);

        // Assert
        result.MaterialCosts[0].PriceSource.Should().Be(PriceSource.Unknown);
        await _universalis.DidNotReceive()
            .GetItemPricesBatchAsync(Arg.Any<string>(), Arg.Any<IEnumerable<int>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GatheredItems_FetchesMBWhenOptionEnabled()
    {
        // Arrange
        var materials = new List<AggregatedMaterial>
        {
            CreateMaterial(300, "Ore", 10, ItemSourceType.Gathered)
        };
        var mbData = CreateMarketBoardData(300, avgNq: 100, minNq: 80);
        _universalis.GetItemPricesBatchAsync("Adamantoise", Arg.Any<IEnumerable<int>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<int, MarketBoardData> { { 300, mbData } });

        var options = new CostEstimationOptions { IncludeGatheredItemPrices = true };

        // Act
        var result = await _service.EstimateCostAsync(materials, null, "Adamantoise", options);

        // Assert
        result.MaterialCosts[0].PriceSource.Should().Be(PriceSource.MarketBoard);
        result.MaterialCosts[0].UnitPrice.Should().Be(100);
    }

    [Fact]
    public async Task ScripItems_MarkedNonGilWithZeroCost()
    {
        // Arrange
        var materials = new List<AggregatedMaterial>
        {
            CreateMaterial(400, "Scrip Mat", 2, ItemSourceType.Scrip)
        };
        var options = new CostEstimationOptions();

        // Act
        var result = await _service.EstimateCostAsync(materials, null, "Adamantoise", options);

        // Assert
        result.MaterialCosts[0].PriceSource.Should().Be(PriceSource.NonGil);
        result.MaterialCosts[0].TotalPrice.Should().Be(0);
    }

    [Fact]
    public async Task MobDropItems_MarkedNonGil()
    {
        // Arrange
        var materials = new List<AggregatedMaterial>
        {
            CreateMaterial(500, "Drop Mat", 1, ItemSourceType.MobDrop)
        };
        var options = new CostEstimationOptions();

        // Act
        var result = await _service.EstimateCostAsync(materials, null, "Adamantoise", options);

        // Assert
        result.MaterialCosts[0].PriceSource.Should().Be(PriceSource.NonGil);
    }

    [Fact]
    public async Task CraftVsBuy_CraftingCheaper_CalculatesSavings()
    {
        // Arrange
        var materials = new List<AggregatedMaterial>
        {
            CreateMaterial(100, "Vendor Mat", 1, ItemSourceType.Vendor, vendorCost: 500)
        };
        var finalItemData = CreateMarketBoardData(999, avgNq: 2000, minNq: 1800);

        _universalis.GetItemPricesBatchAsync("Adamantoise", Arg.Any<IEnumerable<int>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<int, MarketBoardData> { { 999, finalItemData } });

        var options = new CostEstimationOptions { IncludeCraftVsBuyAnalysis = true };

        // Act
        var result = await _service.EstimateCostAsync(materials, 999, "Adamantoise", options);

        // Assert
        result.FinalItemMarketPrice.Should().Be(2000);
        result.CraftingIsCheaper.Should().BeTrue();
        result.Savings.Should().Be(1500); // 2000 - 500
    }

    [Fact]
    public async Task CraftVsBuy_BuyingCheaper_NegativeSavings()
    {
        // Arrange
        var materials = new List<AggregatedMaterial>
        {
            CreateMaterial(100, "Expensive Mat", 10, ItemSourceType.Vendor, vendorCost: 500)
        };
        var finalItemData = CreateMarketBoardData(999, avgNq: 1000, minNq: 800);

        _universalis.GetItemPricesBatchAsync("Adamantoise", Arg.Any<IEnumerable<int>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<int, MarketBoardData> { { 999, finalItemData } });

        var options = new CostEstimationOptions { IncludeCraftVsBuyAnalysis = true };

        // Act
        var result = await _service.EstimateCostAsync(materials, 999, "Adamantoise", options);

        // Assert
        result.TotalEstimatedCost.Should().Be(5000); // 500 * 10
        result.FinalItemMarketPrice.Should().Be(1000);
        result.CraftingIsCheaper.Should().BeFalse();
        result.Savings.Should().Be(-4000); // 1000 - 5000
    }

    [Fact]
    public async Task StaleData_FlaggedCorrectly()
    {
        // Arrange
        var materials = new List<AggregatedMaterial>
        {
            CreateMaterial(200, "Stale Item", 1, ItemSourceType.Other)
        };
        var mbData = CreateMarketBoardData(200, avgNq: 500, minNq: 300,
            lastUploadTime: DateTime.UtcNow.AddHours(-48));

        _universalis.GetItemPricesBatchAsync("Adamantoise", Arg.Any<IEnumerable<int>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<int, MarketBoardData> { { 200, mbData } });

        var options = new CostEstimationOptions();

        // Act
        var result = await _service.EstimateCostAsync(materials, null, "Adamantoise", options);

        // Assert
        result.MaterialCosts[0].IsStalePrice.Should().BeTrue();
    }

    [Fact]
    public async Task CacheHit_SkipsApiCall()
    {
        // Arrange
        var materials = new List<AggregatedMaterial>
        {
            CreateMaterial(200, "Cached Item", 1, ItemSourceType.Other)
        };
        var cachedData = CreateMarketBoardData(200, avgNq: 500, minNq: 300);

        _cache.GetAsync<MarketBoardData>("universalis:Adamantoise:200", Arg.Any<CancellationToken>())
            .Returns(cachedData);

        var options = new CostEstimationOptions();

        // Act
        var result = await _service.EstimateCostAsync(materials, null, "Adamantoise", options);

        // Assert
        result.MaterialCosts[0].PriceSource.Should().Be(PriceSource.MarketBoard);
        result.MaterialCosts[0].UnitPrice.Should().Be(500);
        await _universalis.DidNotReceive()
            .GetItemPricesBatchAsync(Arg.Any<string>(), Arg.Any<IEnumerable<int>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MixedSources_AggregatesCorrectly()
    {
        // Arrange
        var materials = new List<AggregatedMaterial>
        {
            CreateMaterial(100, "Vendor Mat", 5, ItemSourceType.Vendor, vendorCost: 100),
            CreateMaterial(200, "MB Mat", 3, ItemSourceType.Other),
            CreateMaterial(300, "Scrip Mat", 2, ItemSourceType.Scrip),
        };
        var mbData = CreateMarketBoardData(200, avgNq: 1000, minNq: 800);
        _universalis.GetItemPricesBatchAsync("Adamantoise", Arg.Any<IEnumerable<int>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<int, MarketBoardData> { { 200, mbData } });

        var options = new CostEstimationOptions();

        // Act
        var result = await _service.EstimateCostAsync(materials, null, "Adamantoise", options);

        // Assert
        result.TotalVendorCost.Should().Be(500);     // 100 * 5
        result.TotalMarketBoardCost.Should().Be(3000); // 1000 * 3
        result.TotalEstimatedCost.Should().Be(3500);   // 500 + 3000
        result.MaterialCosts.Should().HaveCount(3);
    }

    [Fact]
    public async Task Estimate_SetsWorldAndTimestamp()
    {
        // Arrange
        var materials = new List<AggregatedMaterial>();
        var options = new CostEstimationOptions();

        // Act
        var result = await _service.EstimateCostAsync(materials, null, "Aether", options);

        // Assert
        result.WorldOrDc.Should().Be("Aether");
        result.GeneratedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    // --- Helpers ---

    private static AggregatedMaterial CreateMaterial(
        int itemId, string name, int qty, ItemSourceType sourceType,
        int? vendorCost = null)
    {
        var sources = new List<ItemSourceSummary>();
        if (vendorCost.HasValue)
        {
            sources.Add(new ItemSourceSummary
            {
                SourceType = ItemSourceType.Vendor,
                Description = "Gil vendor",
                Cost = vendorCost.Value,
                CurrencyType = "Gil"
            });
        }

        return new AggregatedMaterial
        {
            ItemId = itemId,
            ItemName = name,
            TotalQuantity = qty,
            PrimarySourceType = sourceType,
            Sources = sources,
        };
    }

    private static MarketBoardData CreateMarketBoardData(
        int itemId, decimal avgNq, decimal minNq,
        DateTime? lastUploadTime = null)
    {
        return new MarketBoardData
        {
            ItemId = itemId,
            AveragePriceNQ = avgNq,
            AveragePriceHQ = avgNq * 1.5m,
            MinPriceNQ = minNq,
            MinPriceHQ = minNq * 1.5m,
            CurrentListingCount = 10,
            LastUploadTime = lastUploadTime ?? DateTime.UtcNow.AddMinutes(-30),
        };
    }
}
