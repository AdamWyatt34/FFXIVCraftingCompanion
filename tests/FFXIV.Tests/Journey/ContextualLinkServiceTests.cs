using FFXIV.Application.Journey;
using FFXIV.Domain.Entities;
using FFXIV.Domain.Enums;
using FFXIV.Domain.Interfaces;
using FFXIV.Domain.Repositories;
using FFXIV.Tests.Gear;
using FFXIV.Tests.Recipes;

namespace FFXIV.Tests.Journey;

public class ContextualLinkServiceTests
{
    private readonly IItemRepository _itemRepo = Substitute.For<IItemRepository>();
    private readonly IRecipeRepository _recipeRepo = Substitute.For<IRecipeRepository>();
    private readonly IEquipmentRepository _equipmentRepo = Substitute.For<IEquipmentRepository>();
    private readonly IGatheringInfoRepository _gatheringRepo = Substitute.For<IGatheringInfoRepository>();
    private readonly IVendorInfoRepository _vendorRepo = Substitute.For<IVendorInfoRepository>();
    private readonly IRecipeLevelRepository _recipeLevelRepo = Substitute.For<IRecipeLevelRepository>();
    private readonly ICacheService _cache = Substitute.For<ICacheService>();
    private readonly ContextualLinkService _service;

    private static readonly Item TestItem = TestDataBuilder.CreateItem(100, "Test Sword");

    public ContextualLinkServiceTests()
    {
        _itemRepo.GetByIdAsync(100, Arg.Any<CancellationToken>()).Returns(TestItem);

        // Default: no results for anything
        _recipeRepo.GetByResultItemIdsAsync(Arg.Any<IEnumerable<int>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Recipe>().AsReadOnly());
        _recipeRepo.GetRecipesUsingIngredientAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<Recipe>().AsReadOnly());
        _equipmentRepo.GetByIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((Equipment?)null);
        _gatheringRepo.GetByItemIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<GatheringInfo>().AsReadOnly());
        _vendorRepo.GetByItemIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<VendorInfo>().AsReadOnly());

        _service = new ContextualLinkService(
            _itemRepo, _recipeRepo, _equipmentRepo,
            _gatheringRepo, _vendorRepo, _recipeLevelRepo, _cache);
    }

    [Fact]
    public async Task CraftableItem_IncludesRecipeAndSimulatorLinks()
    {
        // Arrange
        var recipe = TestDataBuilder.CreateTestSwordRecipe();
        _recipeRepo.GetByResultItemIdsAsync(Arg.Any<IEnumerable<int>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Recipe> { recipe }.AsReadOnly());

        var rlEntry = new RecipeLevelEntry { RecipeLevel = 50, Stars = 2, ClassJobLevel = 50 };
        _recipeLevelRepo.GetByLevelAsync(50, Arg.Any<CancellationToken>()).Returns(rlEntry);

        // Act
        var result = await _service.GetLinksForItemAsync(100);

        // Assert
        result.Recipes.Should().HaveCount(1);
        result.Recipes[0].RecipeId.Should().Be(1);
        result.Recipes[0].Stars.Should().Be(2);
        result.Recipes[0].CraftType.Should().Be("BSM");
        result.CanSimulate.Should().BeTrue();
        result.CanSolve.Should().BeTrue();
    }

    [Fact]
    public async Task GatherableItem_IncludesGatheringInfo()
    {
        // Arrange
        var gatheringInfo = new GatheringInfo
        {
            GatheringInfoId = 1,
            ItemId = 100,
            GatheringType = GatheringType.Mining,
            ClassJob = "MIN",
            Level = 60,
            ZoneName = "The Peaks",
            CoordinateX = 25.5f,
            CoordinateY = 30.2f,
            IsTimed = true,
            SpawnTimes = "8:00, 20:00",
        };
        _gatheringRepo.GetByItemIdAsync(100, Arg.Any<CancellationToken>())
            .Returns(new List<GatheringInfo> { gatheringInfo }.AsReadOnly());

        // Act
        var result = await _service.GetLinksForItemAsync(100);

        // Assert
        result.GatheringInfo.Should().HaveCount(1);
        result.GatheringInfo[0].Zone.Should().Be("The Peaks");
        result.GatheringInfo[0].GatheringClass.Should().Be("MIN");
        result.GatheringInfo[0].GatheringLevel.Should().Be(60);
        result.GatheringInfo[0].IsTimed.Should().BeTrue();
        result.GatheringInfo[0].Coordinates.Should().Be("25.5, 30.2");
    }

    [Fact]
    public async Task VendorItem_IncludesVendorSources()
    {
        // Arrange
        var vendorInfo = new VendorInfo
        {
            VendorInfoId = 1,
            ItemId = 100,
            NpcName = "Material Supplier",
            ZoneName = "Ul'dah",
            Cost = 500,
            CurrencyType = "Gil",
        };
        _vendorRepo.GetByItemIdAsync(100, Arg.Any<CancellationToken>())
            .Returns(new List<VendorInfo> { vendorInfo }.AsReadOnly());

        // Act
        var result = await _service.GetLinksForItemAsync(100);

        // Assert
        result.VendorSources.Should().HaveCount(1);
        result.VendorSources[0].NpcName.Should().Be("Material Supplier");
        result.VendorSources[0].Zone.Should().Be("Ul'dah");
        result.VendorSources[0].Cost.Should().Be(500);
        result.VendorSources[0].Currency.Should().Be("Gil");
    }

    [Fact]
    public async Task EquipmentItem_IncludesEquipmentInfo()
    {
        // Arrange
        var equipment = EquipmentTestDataBuilder.CreateDoHEquipment(
            100, "Test Sword", EquipSlot.MainHand, craftsmanship: 80, control: 60, cp: 10);
        _equipmentRepo.GetByIdAsync(100, Arg.Any<CancellationToken>()).Returns(equipment);

        // Act
        var result = await _service.GetLinksForItemAsync(100);

        // Assert
        result.IsEquipment.Should().BeTrue();
        result.EquipmentInfo.Should().NotBeNull();
        result.EquipmentInfo!.EquipmentId.Should().Be(100);
        result.EquipmentInfo.EquipSlot.Should().Be("MainHand");
        result.EquipmentInfo.Craftsmanship.Should().Be(80);
        result.EquipmentInfo.Control.Should().Be(60);
    }

    [Fact]
    public async Task ItemUsedAsIngredient_IncludesUsedInRecipes()
    {
        // Arrange: item 100 is used in a recipe
        var recipe = TestDataBuilder.CreateRecipe(10, 200, "Big Sword", 1, (100, "Test Sword", 2));
        _recipeRepo.GetRecipesUsingIngredientAsync(100, 20, Arg.Any<CancellationToken>())
            .Returns(new List<Recipe> { recipe }.AsReadOnly());

        var rlEntry = new RecipeLevelEntry { RecipeLevel = 50, Stars = 0, ClassJobLevel = 50 };
        _recipeLevelRepo.GetByLevelAsync(50, Arg.Any<CancellationToken>()).Returns(rlEntry);

        // Act
        var result = await _service.GetLinksForItemAsync(100);

        // Assert
        result.UsedInRecipes.Should().HaveCount(1);
        result.UsedInRecipes[0].RecipeId.Should().Be(10);
        result.UsedInRecipes[0].ResultItemName.Should().Be("Big Sword");
    }

    [Fact]
    public async Task ItemNotFound_ThrowsArgumentException()
    {
        // Arrange
        _itemRepo.GetByIdAsync(999, Arg.Any<CancellationToken>()).Returns((Item?)null);

        // Act & Assert
        var act = () => _service.GetLinksForItemAsync(999);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*999*not found*");
    }
}
