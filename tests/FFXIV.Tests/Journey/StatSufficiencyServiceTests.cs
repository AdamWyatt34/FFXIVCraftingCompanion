using FFXIV.Application.Recipes;
using FFXIV.Application.Gear;
using FFXIV.Application.Journey;
using FFXIV.Domain.Models;
using FFXIV.Domain.Entities;
using FFXIV.Domain.Enums;
using FFXIV.Domain.Interfaces;
using FFXIV.Domain.Repositories;
using FFXIV.Tests.Gear;
using FFXIV.Tests.Recipes;

namespace FFXIV.Tests.Journey;

public class StatSufficiencyServiceTests
{
    private readonly IRecipeRepository _recipeRepo = Substitute.For<IRecipeRepository>();
    private readonly IRecipeParameterFactory _recipeParamFactory = Substitute.For<IRecipeParameterFactory>();
    private readonly IGearRecommendationService _gearService = Substitute.For<IGearRecommendationService>();
    private readonly IEquipmentRepository _equipmentRepo = Substitute.For<IEquipmentRepository>();
    private readonly ICacheService _cache = Substitute.For<ICacheService>();
    private readonly StatSufficiencyService _service;

    private static readonly Recipe TestRecipe = TestDataBuilder.CreateTestSwordRecipe();
    private static readonly RecipeParameters TestRecipeParams = new()
    {
        RecipeId = 1,
        RecipeLevel = 50,
        Difficulty = 1000,
        Quality = 2000,
        Durability = 80,
        ProgressDivider = 100,
        ProgressModifier = 100,
        QualityDivider = 100,
        QualityModifier = 100,
        ClassJobLevel = 50,
        SuggestedCraftsmanship = 400,
        SuggestedControl = 350,
    };

    public StatSufficiencyServiceTests()
    {
        _recipeRepo.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(TestRecipe);
        _recipeParamFactory.CreateAsync(1, Arg.Any<CancellationToken>()).Returns(TestRecipeParams);

        _service = new StatSufficiencyService(
            _recipeRepo, _recipeParamFactory, _gearService, _equipmentRepo, _cache);
    }

    [Fact]
    public async Task Meeting_minimums_keeps_engine_estimates_empty()
    {
        var stats = new CrafterStats { Level = 50, Craftsmanship = 400, Control = 350, CP = 300 };

        var result = await _service.AnalyzeAsync(1, stats);

        result.MeetsMinimumRequirements.Should().BeTrue();
        result.CraftsmanshipDeficit.Should().Be(0);
        result.ControlDeficit.Should().Be(0);
        result.LevelDeficit.Should().Be(0);
        result.EstimatedHQPercent.Should().Be(0);
        result.EstimatedQualityPercent.Should().Be(0);
        result.EstimatedSteps.Should().Be(0);
        result.CanComplete.Should().BeFalse();
        result.Recommendation.Should().Be(StatRecommendation.UpgradeGear);
    }

    [Fact]
    public async Task UndergearedStats_ReturnsUpgradeGear()
    {
        // Arrange: stats below requirements
        var stats = new CrafterStats { Level = 50, Craftsmanship = 300, Control = 200, CP = 400 };

        // Act
        var result = await _service.AnalyzeAsync(1, stats);

        // Assert
        result.MeetsMinimumRequirements.Should().BeFalse();
        result.CraftsmanshipSufficient.Should().BeFalse();
        result.ControlSufficient.Should().BeFalse();
        result.CraftsmanshipDeficit.Should().Be(100); // 400 - 300
        result.ControlDeficit.Should().Be(150);         // 350 - 200
        result.Recommendation.Should().Be(StatRecommendation.UpgradeGear);
    }

    [Fact]
    public async Task GearSuggestions_WithProfile_ReturnsUpgrades()
    {
        // Arrange
        var stats = new CrafterStats { Level = 90, Craftsmanship = 3000, Control = 2800, CP = 500 };

        var profile = new CharacterProfile
        {
            Id = 1,
            UserId = "user1",
            CharacterName = "Test",
            ActiveJob = "BSM",
            ActiveJobLevel = 90,
            Craftsmanship = 3000,
            Control = 2800,
            CP = 500,
            CurrentGear = new Dictionary<string, int> { ["Head"] = 10 },
        };

        var currentHead = EquipmentTestDataBuilder.CreateDoHEquipment(
            10, "Old Hat", EquipSlot.Head, craftsmanship: 80, control: 70, cp: 5);
        _equipmentRepo.GetByIdsAsync(Arg.Any<IEnumerable<int>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Equipment> { currentHead }.AsReadOnly());

        var betterHead = EquipmentTestDataBuilder.CreateDoHEquipment(
            20, "Better Hat", EquipSlot.Head, craftsmanship: 120, control: 100, cp: 10);
        var recommendedSet = new GearSet
        {
            Job = "BSM",
            Level = 90,
            Slots =
            [
                new GearSlotRecommendation
                {
                    Slot = EquipSlot.Head,
                    SlotLabel = "Head",
                    RecommendedItem = betterHead,
                    Alternatives = [],
                    Source = new EquipmentSourceInfo
                    {
                        SourceType = ItemSourceType.Crafted,
                        RecipeId = 99,
                        Description = "Crafted",
                    },
                    WeightedScore = 200,
                },
            ],
            TotalStats = new GearStats(),
        };
        _gearService.GetRecommendationsAsync("BSM", 90, "balanced", Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(recommendedSet);

        // Act
        var result = await _service.AnalyzeAsync(1, stats, profile);

        // Assert
        result.SuggestedGearUpgrades.Should().HaveCount(1);
        var upgrade = result.SuggestedGearUpgrades[0];
        upgrade.Slot.Should().Be("Head");
        upgrade.CurrentItem.Should().Be("Old Hat");
        upgrade.SuggestedItem.Should().Be("Better Hat");
        upgrade.CraftsmanshipGain.Should().Be(40);
        upgrade.ControlGain.Should().Be(30);
        upgrade.CPGain.Should().Be(5);
        upgrade.RecipeId.Should().Be(99);
    }

    [Fact]
    public async Task GearSuggestions_WithoutProfile_ReturnsEmpty()
    {
        // Arrange
        var stats = new CrafterStats { Level = 90, Craftsmanship = 4000, Control = 3800, CP = 600 };

        // Act — no profile passed
        var result = await _service.AnalyzeAsync(1, stats);

        // Assert
        result.SuggestedGearUpgrades.Should().BeEmpty();
    }
}
