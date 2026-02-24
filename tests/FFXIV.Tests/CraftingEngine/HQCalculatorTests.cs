using FFXIV.Domain.CraftingEngine;
using FFXIV.Domain.CraftingEngine.Models;
using FluentAssertions;

namespace FFXIV.Tests.CraftingEngine;

public class HQCalculatorTests
{
    [Fact]
    public void CalculateStartingQuality_NoHQIngredients_ReturnsZero()
    {
        var ingredients = new List<RecipeIngredientInfo>
        {
            new(100, 3, true),
            new(200, 2, true),
        };
        var hqInputs = new List<HQIngredientInput>();

        var result = HQCalculator.CalculateStartingQuality(10000, 75, ingredients, hqInputs);

        result.Should().Be(0);
    }

    [Fact]
    public void CalculateStartingQuality_AllHQ_ReturnsMaxStartingQuality()
    {
        var ingredients = new List<RecipeIngredientInfo>
        {
            new(100, 3, true),
            new(200, 2, true),
        };
        var hqInputs = new List<HQIngredientInput>
        {
            new(100, 3),
            new(200, 2),
        };

        var result = HQCalculator.CalculateStartingQuality(10000, 75, ingredients, hqInputs);

        // maxStartingQuality = floor(10000 * 75 / 100) = 7500
        // totalHQable = 3 + 2 = 5, totalHQ = 3 + 2 = 5
        // result = floor(7500 * 5 / 5) = 7500
        result.Should().Be(7500);
    }

    [Fact]
    public void CalculateStartingQuality_PartialHQ_ReturnsProportionalQuality()
    {
        var ingredients = new List<RecipeIngredientInfo>
        {
            new(100, 4, true),
            new(200, 2, true),
        };
        var hqInputs = new List<HQIngredientInput>
        {
            new(100, 2), // 2 of 4 HQ
        };

        var result = HQCalculator.CalculateStartingQuality(10000, 80, ingredients, hqInputs);

        // maxStartingQuality = floor(10000 * 80 / 100) = 8000
        // totalHQable = 4 + 2 = 6, totalHQ = 2
        // result = floor(8000 * 2 / 6) = floor(2666.67) = 2666
        result.Should().Be(2666);
    }

    [Fact]
    public void CalculateStartingQuality_NonHQableIngredientsIgnored()
    {
        var ingredients = new List<RecipeIngredientInfo>
        {
            new(100, 3, true),
            new(200, 6, false), // Crystals, not HQ-able
        };
        var hqInputs = new List<HQIngredientInput>
        {
            new(100, 3),
            new(200, 6), // Should be ignored because ingredient is not HQ-able
        };

        var result = HQCalculator.CalculateStartingQuality(10000, 75, ingredients, hqInputs);

        // maxStartingQuality = floor(10000 * 75 / 100) = 7500
        // totalHQable = 3 (only item 100), totalHQ = 3
        // result = floor(7500 * 3 / 3) = 7500
        result.Should().Be(7500);
    }

    [Fact]
    public void CalculateStartingQuality_MaterialQualityFactorZero_ReturnsZero()
    {
        var ingredients = new List<RecipeIngredientInfo>
        {
            new(100, 3, true),
        };
        var hqInputs = new List<HQIngredientInput>
        {
            new(100, 3),
        };

        var result = HQCalculator.CalculateStartingQuality(10000, 0, ingredients, hqInputs);

        result.Should().Be(0);
    }

    [Fact]
    public void CalculateStartingQuality_NoHQableIngredients_ReturnsZero()
    {
        var ingredients = new List<RecipeIngredientInfo>
        {
            new(100, 3, false),
            new(200, 6, false),
        };
        var hqInputs = new List<HQIngredientInput>
        {
            new(100, 3),
        };

        var result = HQCalculator.CalculateStartingQuality(10000, 75, ingredients, hqInputs);

        result.Should().Be(0);
    }

    [Fact]
    public void CalculateStartingQuality_NullHQInputs_ReturnsZero()
    {
        var ingredients = new List<RecipeIngredientInfo>
        {
            new(100, 3, true),
        };

        var result = HQCalculator.CalculateStartingQuality(10000, 75, ingredients, null!);

        result.Should().Be(0);
    }

    [Fact]
    public void CalculateStartingQuality_HQQuantityExceedsMax_ClampsToIngredientQuantity()
    {
        var ingredients = new List<RecipeIngredientInfo>
        {
            new(100, 3, true),
        };
        var hqInputs = new List<HQIngredientInput>
        {
            new(100, 99), // Way more than needed
        };

        var result = HQCalculator.CalculateStartingQuality(10000, 50, ingredients, hqInputs);

        // maxStartingQuality = floor(10000 * 50 / 100) = 5000
        // totalHQable = 3, totalHQ = min(99, 3) = 3
        // result = floor(5000 * 3 / 3) = 5000
        result.Should().Be(5000);
    }

    [Fact]
    public void CalculateStartingQuality_MaxQualityZero_ReturnsZero()
    {
        var ingredients = new List<RecipeIngredientInfo>
        {
            new(100, 3, true),
        };
        var hqInputs = new List<HQIngredientInput>
        {
            new(100, 3),
        };

        var result = HQCalculator.CalculateStartingQuality(0, 75, ingredients, hqInputs);

        result.Should().Be(0);
    }
}
