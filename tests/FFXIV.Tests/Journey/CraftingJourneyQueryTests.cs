using FFXIV.Application.Journey;
using FFXIV.Application.Journey.Queries;
using FFXIV.Application.Recipes;
using FFXIV.Domain.Models;
using FFXIV.Domain.Entities;
using FFXIV.Domain.Enums;
using FFXIV.Domain.Repositories;
using FFXIV.Tests.Recipes;
using Microsoft.Extensions.Logging;

namespace FFXIV.Tests.Journey;

public class CraftingJourneyQueryTests
{
    private readonly IRecipeRepository _recipeRepo = Substitute.For<IRecipeRepository>();
    private readonly IEquipmentRepository _equipmentRepo = Substitute.For<IEquipmentRepository>();
    private readonly IRecipeTreeResolver _treeResolver = Substitute.For<IRecipeTreeResolver>();
    private readonly IContextualLinkService _linkService = Substitute.For<IContextualLinkService>();
    private readonly IStatSufficiencyService _sufficiencyService = Substitute.For<IStatSufficiencyService>();
    private readonly ICharacterProfileRepository _profileRepo = Substitute.For<ICharacterProfileRepository>();
    private readonly ILogger<GetCraftingJourneyQueryHandler> _logger = Substitute.For<ILogger<GetCraftingJourneyQueryHandler>>();
    private readonly GetCraftingJourneyQueryHandler _handler;

    private static readonly Recipe TestRecipe = TestDataBuilder.CreateTestSwordRecipe();

    public CraftingJourneyQueryTests()
    {
        _recipeRepo.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(TestRecipe);
        _equipmentRepo.GetByRecipeIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((Equipment?)null);

        var treeResult = new MaterialTreeResult
        {
            RootItem = new MaterialTreeNode
            {
                ItemId = 100,
                ItemName = "Test Sword",
                IconUrl = "icon.png",
                Quantity = 1,
                IsRawMaterial = false,
                SourceType = ItemSourceType.Crafted,
                Children = [],
                Sources = [],
            },
            ShoppingList = [],
            TotalUniqueItems = 1,
            TreeDepth = 1,
            CrystalRequirements = [],
            IntermediateCrafts = [],
        };
        _treeResolver.ResolveTreeAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(treeResult);

        var contextLinks = new ContextualLinks
        {
            ItemId = 100,
            ItemName = "Test Sword",
            IconUrl = "icon.png",
            Recipes = [new RecipeLink { RecipeId = 1, ResultItemName = "Test Sword", CraftType = "BSM", Level = 50 }],
            CanSimulate = true,
            CanSolve = true,
            UsedInRecipes = [],
            GatheringInfo = [],
            VendorSources = [],
        };
        _linkService.GetLinksForItemAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(contextLinks);

        _handler = new GetCraftingJourneyQueryHandler(
            _recipeRepo, _equipmentRepo, _treeResolver, _linkService,
            _sufficiencyService, _profileRepo, _logger);
    }

    [Fact]
    public async Task ValidRecipe_AnonymousUser_ReturnsJourneyWithoutSufficiency()
    {
        // Act
        var (result, error) = await _handler.Handle(
            new GetCraftingJourneyQuery(1, null), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        error.Should().BeNull();
        result!.Recipe.RecipeId.Should().Be(1);
        result.Materials.Should().NotBeNull();
        result.ItemContext.Should().NotBeNull();
        result.Sufficiency.Should().BeNull();

        // Verify sufficiency service was NOT called
        await _sufficiencyService.DidNotReceive().AnalyzeAsync(
            Arg.Any<int>(), Arg.Any<CrafterStats>(),
            Arg.Any<CharacterProfile?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ValidRecipe_AuthenticatedUser_ReturnsJourneyWithSufficiency()
    {
        // Arrange
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
        };
        _profileRepo.GetByUserIdAsync("user1", Arg.Any<CancellationToken>()).Returns(profile);

        var sufficiency = new StatSufficiencyResult
        {
            RecipeId = 1,
            MeetsMinimumRequirements = true,
            CraftsmanshipSufficient = true,
            ControlSufficient = true,
            LevelSufficient = true,
            CanComplete = true,
            EstimatedHQPercent = 90,
            EstimatedQualityPercent = 95,
            EstimatedSteps = 10,
            Recommendation = StatRecommendation.StatsExcellent,
        };
        _sufficiencyService.AnalyzeAsync(1, Arg.Any<CrafterStats>(),
            Arg.Any<CharacterProfile?>(), Arg.Any<CancellationToken>())
            .Returns(sufficiency);

        // Act
        var (result, error) = await _handler.Handle(
            new GetCraftingJourneyQuery(1, "user1"), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        error.Should().BeNull();
        result!.Sufficiency.Should().NotBeNull();
        result.Sufficiency!.MeetsMinimumRequirements.Should().BeTrue();
        result.Sufficiency.Recommendation.Should().Be("StatsExcellent");
        result.Sufficiency.EstimatedHQPercent.Should().Be(90);
    }

    [Fact]
    public async Task RecipeNotFound_ReturnsError()
    {
        // Arrange
        _recipeRepo.GetByIdAsync(999, Arg.Any<CancellationToken>()).Returns((Recipe?)null);

        // Act
        var (result, error) = await _handler.Handle(
            new GetCraftingJourneyQuery(999, null), CancellationToken.None);

        // Assert
        result.Should().BeNull();
        error.Should().Contain("999");
        error.Should().Contain("not found");
    }
}
