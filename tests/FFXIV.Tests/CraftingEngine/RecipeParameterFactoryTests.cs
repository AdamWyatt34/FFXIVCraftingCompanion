using FFXIV.Application.CraftingEngine;
using FFXIV.Domain.CraftingEngine.Models;
using FFXIV.Domain.Entities;
using FFXIV.Domain.Enums;
using FFXIV.Domain.Repositories;

namespace FFXIV.Tests.CraftingEngine;

public class RecipeParameterFactoryTests
{
    private readonly IRecipeRepository _recipeRepo = Substitute.For<IRecipeRepository>();
    private readonly IRecipeLevelRepository _rltRepo = Substitute.For<IRecipeLevelRepository>();
    private readonly RecipeParameterFactory _factory;

    public RecipeParameterFactoryTests()
    {
        _factory = new RecipeParameterFactory(_recipeRepo, _rltRepo);
    }

    [Fact]
    public async Task CreateAsync_ValidRecipeAndRlt_ReturnsPopulatedParameters()
    {
        var recipe = new Recipe
        {
            RecipeId = 100,
            ResultItemId = 500,
            CraftType = CraftType.CRP,
            RecipeLevel = 580,
            Difficulty = 3500,
            Quality = 7200,
            Durability = 70,
            RequiredCraftsmanship = 3700,
            RequiredControl = 3280,
            Yield = 1,
            Ingredients =
            [
                new RecipeIngredient
                {
                    // The sync leaves the ingredient's own flag false; the Item says whether it can be HQ.
                    RecipeId = 100, ItemId = 37830, Quantity = 2, CanBeHQ = false,
                    Item = new Item { ItemId = 37830, Name = "Rutilated Quartz", ItemLevel = 610, CanBeHQ = true },
                },
                new RecipeIngredient
                {
                    RecipeId = 100, ItemId = 15, Quantity = 5, CanBeHQ = false,
                    Item = new Item { ItemId = 15, Name = "Ice Cluster", ItemLevel = 1, CanBeHQ = false },
                },
            ],
        };
        var rlt = new RecipeLevelEntry
        {
            RecipeLevel = 580,
            ClassJobLevel = 90,
            Stars = 2,
            Difficulty = 5000,
            Quality = 10000,
            Durability = 70,
            SuggestedCraftsmanship = 4045,
            SuggestedControl = 3792,
            ProgressDivider = 130,
            ProgressModifier = 80,
            QualityDivider = 115,
            QualityModifier = 70,
        };

        _recipeRepo.GetByIdAsync(100, Arg.Any<CancellationToken>()).Returns(recipe);
        _rltRepo.GetByLevelAsync(580, Arg.Any<CancellationToken>()).Returns(rlt);

        var result = await _factory.CreateAsync(100);

        result.Should().NotBeNull();
        result!.RecipeId.Should().Be(100);
        result.RecipeLevel.Should().Be(580);
        result.Difficulty.Should().Be(3500);
        result.Quality.Should().Be(7200);
        result.Durability.Should().Be(70);
        result.ProgressDivider.Should().Be(130);
        result.ProgressModifier.Should().Be(80);
        result.QualityDivider.Should().Be(115);
        result.QualityModifier.Should().Be(70);
        result.Stars.Should().Be(2);
        result.SuggestedCraftsmanship.Should().Be(4045);
        result.SuggestedControl.Should().Be(3792);
        result.RequiredCraftsmanship.Should().Be(3700);
        result.RequiredControl.Should().Be(3280);
        result.Ingredients.Should().Equal(
            new RecipeIngredientInfo(37830, 2, true, 610),
            new RecipeIngredientInfo(15, 5, false, 1));
    }

    [Fact]
    public async Task CreateAsync_RecipeNotFound_ReturnsNull()
    {
        _recipeRepo.GetByIdAsync(999, Arg.Any<CancellationToken>()).Returns((Recipe?)null);

        var result = await _factory.CreateAsync(999);

        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_RltNotFound_ReturnsNull()
    {
        var recipe = new Recipe
        {
            RecipeId = 100,
            ResultItemId = 500,
            RecipeLevel = 999,
        };
        _recipeRepo.GetByIdAsync(100, Arg.Any<CancellationToken>()).Returns(recipe);
        _rltRepo.GetByLevelAsync(999, Arg.Any<CancellationToken>()).Returns((RecipeLevelEntry?)null);

        var result = await _factory.CreateAsync(100);

        result.Should().BeNull();
    }
}
