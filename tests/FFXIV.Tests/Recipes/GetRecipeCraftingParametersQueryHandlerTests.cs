using FFXIV.Application.Recipes;
using FFXIV.Application.Recipes.Queries;
using FFXIV.Domain.Models;

namespace FFXIV.Tests.Recipes;

public class GetRecipeCraftingParametersQueryHandlerTests
{
    private readonly IRecipeParameterFactory _factory = Substitute.For<IRecipeParameterFactory>();
    private readonly GetRecipeCraftingParametersQueryHandler _handler;

    public GetRecipeCraftingParametersQueryHandlerTests()
    {
        _handler = new GetRecipeCraftingParametersQueryHandler(_factory);
    }

    [Fact]
    public async Task Returns_null_when_the_recipe_is_unknown()
    {
        _factory.CreateAsync(42, Arg.Any<CancellationToken>()).Returns((RecipeParameters?)null);

        var result = await _handler.Handle(new GetRecipeCraftingParametersQuery(42), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Maps_every_engine_parameter_and_the_ingredients()
    {
        _factory.CreateAsync(100, Arg.Any<CancellationToken>()).Returns(new RecipeParameters
        {
            RecipeId = 100,
            RecipeLevel = 580,
            Difficulty = 3500,
            Quality = 7200,
            Durability = 70,
            ProgressDivider = 130,
            ProgressModifier = 80,
            QualityDivider = 115,
            QualityModifier = 70,
            ClassJobLevel = 90,
            Stars = 2,
            SuggestedCraftsmanship = 3700,
            SuggestedControl = 3280,
            RequiredCraftsmanship = 2900,
            RequiredControl = 2700,
            MaterialQualityFactor = 75,
            Ingredients =
            [
                new RecipeIngredientInfo(1, 2, true, 640),
                new RecipeIngredientInfo(2, 1, false, 1),
            ],
        });

        var result = await _handler.Handle(new GetRecipeCraftingParametersQuery(100), CancellationToken.None);

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
        result.ClassJobLevel.Should().Be(90);
        result.Stars.Should().Be(2);
        result.IsExpert.Should().BeFalse();
        result.ConditionsFlag.Should().Be(0);
        result.SuggestedCraftsmanship.Should().Be(3700);
        result.SuggestedControl.Should().Be(3280);
        result.RequiredCraftsmanship.Should().Be(2900);
        result.RequiredControl.Should().Be(2700);
        result.MaterialQualityFactor.Should().Be(75);
        result.Ingredients.Should().HaveCount(2);
        result.Ingredients[0].Should().Be(new FFXIV.Application.Recipes.DTOs.RecipeCraftingIngredientDto(1, 2, true, 640));
        result.Ingredients[1].CanBeHQ.Should().BeFalse();
        result.Ingredients[1].ItemLevel.Should().Be(1);
    }
}
