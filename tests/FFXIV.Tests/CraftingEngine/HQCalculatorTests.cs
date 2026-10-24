using FFXIV.Domain.CraftingEngine;
using FFXIV.Domain.CraftingEngine.Models;
using FluentAssertions;

namespace FFXIV.Tests.CraftingEngine;

public class HQCalculatorTests
{
    private const int ItemLevel = 560;

    // Recipe 35423 (Indagator's Alembic): max quality 12900, material quality factor 50.
    private const int Alkahest = 38932;
    private const int Ilmenite = 37833;
    private const int Quartz = 37830;

    private static List<RecipeIngredientInfo> Recipe35423(bool withItemLevels) =>
    [
        new(15, 5, false, withItemLevels ? 1 : 0),
        new(17, 5, false, withItemLevels ? 1 : 0),
        new(Quartz, 2, true, withItemLevels ? 610 : 0),
        new(Ilmenite, 3, true, withItemLevels ? 610 : 0),
        new(Alkahest, 3, true, withItemLevels ? 620 : 0),
        new(39595, 3, false, withItemLevels ? 620 : 0),
    ];

    [Fact]
    public void CalculateStartingQuality_NoHQIngredients_ReturnsZero()
    {
        var ingredients = new List<RecipeIngredientInfo>
        {
            new(100, 3, true, ItemLevel),
            new(200, 2, true, ItemLevel),
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
            new(100, 3, true, ItemLevel),
            new(200, 2, true, ItemLevel),
        };
        var hqInputs = new List<HQIngredientInput>
        {
            new(100, 3),
            new(200, 2),
        };

        var result = HQCalculator.CalculateStartingQuality(10000, 75, ingredients, hqInputs);

        // 10000 * 75 * (5 * 560) / (5 * 560) / 100 = 7500
        result.Should().Be(7500);
    }

    [Fact]
    public void CalculateStartingQuality_PartialHQ_EqualItemLevels_ReturnsProportionalQuality()
    {
        var ingredients = new List<RecipeIngredientInfo>
        {
            new(100, 4, true, ItemLevel),
            new(200, 2, true, ItemLevel),
        };
        var hqInputs = new List<HQIngredientInput>
        {
            new(100, 2), // 2 of 4 HQ
        };

        var result = HQCalculator.CalculateStartingQuality(10000, 80, ingredients, hqInputs);

        // 10000 * 80 * (2 * 560) / (6 * 560) / 100 = 2666.7 → 2666
        result.Should().Be(2666);
    }

    // Raphael raphael-data/src/lib.rs:203-239; Teamcraft recipes.extractor.ts:60-73,100-106: weighted by item level.
    [Theory]
    [InlineData(new[] { Alkahest }, new[] { 3 }, 2443)]
    [InlineData(new[] { Quartz }, new[] { 2 }, 1602)]
    [InlineData(new[] { Alkahest, Ilmenite, Quartz }, new[] { 3, 3, 2 }, 6450)]
    [InlineData(new int[0], new int[0], 0)]
    public void CalculateStartingQuality_WeightsByItemLevel(int[] hqItems, int[] hqQuantities, int expected)
    {
        var hqInputs = hqItems.Zip(hqQuantities, (item, qty) => new HQIngredientInput(item, qty)).ToList();

        var result = HQCalculator.CalculateStartingQuality(12900, 50, Recipe35423(withItemLevels: true), hqInputs);

        result.Should().Be(expected);
    }

    [Fact]
    public void CalculateStartingQuality_NoItemLevels_WeightsByCount()
    {
        var hqInputs = new List<HQIngredientInput> { new(Alkahest, 3) };

        var result = HQCalculator.CalculateStartingQuality(12900, 50, Recipe35423(withItemLevels: false), hqInputs);

        // 3 of 8 HQ-able materials: 12900 * 50 * 3 / 8 / 100 = 2418.75 → 2418
        result.Should().Be(2418);
    }

    [Fact]
    public void CalculateStartingQuality_NonHQableIngredientsIgnored()
    {
        var ingredients = new List<RecipeIngredientInfo>
        {
            new(100, 3, true, ItemLevel),
            new(200, 6, false, 1),
        };
        var hqInputs = new List<HQIngredientInput>
        {
            new(100, 3),
            new(200, 6), // Should be ignored because ingredient is not HQ-able
        };

        var result = HQCalculator.CalculateStartingQuality(10000, 75, ingredients, hqInputs);

        // Only item 100 is HQ-able and all of it is HQ: 7500
        result.Should().Be(7500);
    }

    [Fact]
    public void CalculateStartingQuality_MaterialQualityFactorZero_ReturnsZero()
    {
        var ingredients = new List<RecipeIngredientInfo>
        {
            new(100, 3, true, ItemLevel),
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
            new(100, 3, false, ItemLevel),
            new(200, 6, false, ItemLevel),
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
            new(100, 3, true, ItemLevel),
        };

        var result = HQCalculator.CalculateStartingQuality(10000, 75, ingredients, null!);

        result.Should().Be(0);
    }

    [Fact]
    public void CalculateStartingQuality_HQQuantityExceedsMax_ClampsToIngredientQuantity()
    {
        var ingredients = new List<RecipeIngredientInfo>
        {
            new(100, 3, true, ItemLevel),
        };
        var hqInputs = new List<HQIngredientInput>
        {
            new(100, 99), // Way more than needed
        };

        var result = HQCalculator.CalculateStartingQuality(10000, 50, ingredients, hqInputs);

        // HQ quantity clamps to 3 of 3: 10000 * 50 / 100 = 5000
        result.Should().Be(5000);
    }

    [Fact]
    public void CalculateStartingQuality_MaxQualityZero_ReturnsZero()
    {
        var ingredients = new List<RecipeIngredientInfo>
        {
            new(100, 3, true, ItemLevel),
        };
        var hqInputs = new List<HQIngredientInput>
        {
            new(100, 3),
        };

        var result = HQCalculator.CalculateStartingQuality(0, 75, ingredients, hqInputs);

        result.Should().Be(0);
    }
}
