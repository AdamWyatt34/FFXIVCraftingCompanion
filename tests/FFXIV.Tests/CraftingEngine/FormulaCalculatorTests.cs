using FFXIV.Domain.CraftingEngine;

namespace FFXIV.Tests.CraftingEngine;

public class FormulaCalculatorTests
{
    [Fact]
    public void CalculateBaseProgress_StandardValues_ReturnsExpected()
    {
        // Craftsmanship=3800, Divider=130 → 3800*10/130 + 2 = 292 + 2 = 294
        // Level 90 <= RecipeLevel 580 → 294 * 80 / 100 = 235
        var result = FormulaCalculator.CalculateBaseProgress(3800, 130, 80, 90, 580);
        result.Should().Be(235);
    }

    [Fact]
    public void CalculateBaseProgress_CrafterLevelAboveRecipe_SkipsModifier()
    {
        // Level 100 > RecipeLevel 90 → modifier NOT applied
        // 3800*10/130 + 2 = 294
        var result = FormulaCalculator.CalculateBaseProgress(3800, 130, 80, 100, 90);
        result.Should().Be(294);
    }

    [Fact]
    public void CalculateBaseProgress_CrafterLevelEqualToRecipe_AppliesModifier()
    {
        // Level 90 == RecipeLevel 90 → modifier applied
        var result = FormulaCalculator.CalculateBaseProgress(3800, 130, 80, 90, 90);
        result.Should().Be(235);
    }

    [Fact]
    public void CalculateBaseProgress_IntegerDivisionTruncates()
    {
        // 1000*10/130 = 76 (truncated from 76.9...) + 2 = 78
        // Level 100 > RecipeLevel 50 → no modifier
        var result = FormulaCalculator.CalculateBaseProgress(1000, 130, 80, 100, 50);
        result.Should().Be(78);
    }

    [Fact]
    public void CalculateBaseQuality_StandardValues_ReturnsExpected()
    {
        // Control=3600, Divider=115 → 3600*10/115 + 35 = 313 + 35 = 348
        // Level 90 <= RecipeLevel 580 → 348 * 70 / 100 = 243
        var result = FormulaCalculator.CalculateBaseQuality(3600, 115, 70, 90, 580);
        result.Should().Be(243);
    }

    [Fact]
    public void CalculateBaseQuality_CrafterLevelAboveRecipe_SkipsModifier()
    {
        // Level 100 > RecipeLevel 90 → modifier NOT applied
        // 3600*10/115 + 35 = 313 + 35 = 348
        var result = FormulaCalculator.CalculateBaseQuality(3600, 115, 70, 100, 90);
        result.Should().Be(348);
    }

    [Fact]
    public void CalculateBaseQuality_IntegerDivisionTruncates()
    {
        // 1000*10/115 = 86 (truncated from 86.9...) + 35 = 121
        var result = FormulaCalculator.CalculateBaseQuality(1000, 115, 70, 100, 50);
        result.Should().Be(121);
    }
}
