using FFXIV.Application.CraftingEngine;
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
            RequiredControl = 0,
            Yield = 1,
        };
        var rlt = new RecipeLevelEntry
        {
            RecipeLevel = 580,
            ClassJobLevel = 90,
            Stars = 2,
            Difficulty = 5000,
            Quality = 10000,
            Durability = 70,
            SuggestedCraftsmanship = 3700,
            SuggestedControl = 0,
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
        result.SuggestedCraftsmanship.Should().Be(3700);
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
