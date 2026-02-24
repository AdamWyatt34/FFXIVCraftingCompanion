using FFXIV.Application.Journey;
using FFXIV.Domain.Entities;
using FFXIV.Domain.Interfaces;
using FFXIV.Domain.Models;
using FFXIV.Domain.Repositories;

namespace FFXIV.Tests.Journey;

public class ContextualLinkCachingTests
{
    private readonly IItemRepository _itemRepo = Substitute.For<IItemRepository>();
    private readonly IRecipeRepository _recipeRepo = Substitute.For<IRecipeRepository>();
    private readonly IEquipmentRepository _equipmentRepo = Substitute.For<IEquipmentRepository>();
    private readonly IGatheringInfoRepository _gatheringRepo = Substitute.For<IGatheringInfoRepository>();
    private readonly IVendorInfoRepository _vendorRepo = Substitute.For<IVendorInfoRepository>();
    private readonly IRecipeLevelRepository _recipeLevelRepo = Substitute.For<IRecipeLevelRepository>();
    private readonly ICacheService _cache = Substitute.For<ICacheService>();
    private readonly ContextualLinkService _service;

    public ContextualLinkCachingTests()
    {
        _service = new ContextualLinkService(
            _itemRepo, _recipeRepo, _equipmentRepo,
            _gatheringRepo, _vendorRepo, _recipeLevelRepo, _cache);
    }

    [Fact]
    public async Task GetLinksForItemAsync_SecondCall_ServedFromCache()
    {
        // Arrange
        var item = new Item { ItemId = 42, Name = "Test Item", IconPath = "/icon/42.png" };
        _itemRepo.GetByIdAsync(42, Arg.Any<CancellationToken>()).Returns(item);
        _recipeRepo.GetByResultItemIdsAsync(Arg.Any<IEnumerable<int>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Recipe>().AsReadOnly());
        _recipeRepo.GetRecipesUsingIngredientAsync(42, 20, Arg.Any<CancellationToken>())
            .Returns(new List<Recipe>().AsReadOnly());
        _equipmentRepo.GetByIdAsync(42, Arg.Any<CancellationToken>())
            .Returns((Equipment?)null);
        _gatheringRepo.GetByItemIdAsync(42, Arg.Any<CancellationToken>())
            .Returns(new List<GatheringInfo>().AsReadOnly());
        _vendorRepo.GetByItemIdAsync(42, Arg.Any<CancellationToken>())
            .Returns(new List<VendorInfo>().AsReadOnly());

        // First call — cache miss
        _cache.GetAsync<ContextualLinks>("contextlinks:42", Arg.Any<CancellationToken>())
            .Returns((ContextualLinks?)null);

        await _service.GetLinksForItemAsync(42);

        // Verify repos were called
        await _itemRepo.Received(1).GetByIdAsync(42, Arg.Any<CancellationToken>());

        // Verify cache was set
        await _cache.Received(1).SetAsync(
            "contextlinks:42",
            Arg.Any<ContextualLinks>(),
            TimeSpan.FromHours(1),
            Arg.Any<CancellationToken>());

        // Second call — cache hit
        _cache.GetAsync<ContextualLinks>("contextlinks:42", Arg.Any<CancellationToken>())
            .Returns(new ContextualLinks
            {
                ItemId = 42,
                ItemName = "Test Item",
                IconUrl = "/icon/42.png",
                Recipes = [],
                UsedInRecipes = [],
                GatheringInfo = [],
                VendorSources = [],
            });

        var result = await _service.GetLinksForItemAsync(42);

        // Assert — repos NOT called a second time (still 1 total call)
        await _itemRepo.Received(1).GetByIdAsync(42, Arg.Any<CancellationToken>());
        result.ItemId.Should().Be(42);
    }

    [Fact]
    public async Task GetLinksForItemAsync_CacheMiss_QueriesAllRepos()
    {
        // Arrange
        var item = new Item { ItemId = 99, Name = "Test", IconPath = "/icon/99.png" };
        _itemRepo.GetByIdAsync(99, Arg.Any<CancellationToken>()).Returns(item);
        _recipeRepo.GetByResultItemIdsAsync(Arg.Any<IEnumerable<int>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Recipe>().AsReadOnly());
        _recipeRepo.GetRecipesUsingIngredientAsync(99, 20, Arg.Any<CancellationToken>())
            .Returns(new List<Recipe>().AsReadOnly());
        _equipmentRepo.GetByIdAsync(99, Arg.Any<CancellationToken>())
            .Returns((Equipment?)null);
        _gatheringRepo.GetByItemIdAsync(99, Arg.Any<CancellationToken>())
            .Returns(new List<GatheringInfo>().AsReadOnly());
        _vendorRepo.GetByItemIdAsync(99, Arg.Any<CancellationToken>())
            .Returns(new List<VendorInfo>().AsReadOnly());
        _cache.GetAsync<ContextualLinks>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((ContextualLinks?)null);

        // Act
        var result = await _service.GetLinksForItemAsync(99);

        // Assert
        result.ItemId.Should().Be(99);
        await _recipeRepo.Received(1).GetByResultItemIdsAsync(
            Arg.Any<IEnumerable<int>>(), Arg.Any<CancellationToken>());
        await _recipeRepo.Received(1).GetRecipesUsingIngredientAsync(
            99, 20, Arg.Any<CancellationToken>());
        await _gatheringRepo.Received(1).GetByItemIdAsync(99, Arg.Any<CancellationToken>());
        await _vendorRepo.Received(1).GetByItemIdAsync(99, Arg.Any<CancellationToken>());
    }
}
