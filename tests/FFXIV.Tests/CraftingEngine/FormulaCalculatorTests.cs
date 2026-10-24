using FFXIV.Domain.CraftingEngine;

namespace FFXIV.Tests.CraftingEngine;

public class FormulaCalculatorTests
{
    [Fact]
    public void CalculateBaseProgress_CrafterBelowClassJobLevel_AppliesModifier()
    {
        // (3800*10/130 + 2) * 80/100 = 235.4 → 235
        var result = FormulaCalculator.CalculateBaseProgress(3800, 130, 80, 80, 90);
        result.Should().Be(235);
    }

    [Fact]
    public void CalculateBaseProgress_CrafterAboveClassJobLevel_SkipsModifier()
    {
        // 3800*10/130 + 2 = 294.3 → 294
        var result = FormulaCalculator.CalculateBaseProgress(3800, 130, 80, 100, 90);
        result.Should().Be(294);
    }

    [Fact]
    public void CalculateBaseProgress_CrafterEqualToClassJobLevel_AppliesModifier()
    {
        var result = FormulaCalculator.CalculateBaseProgress(3800, 130, 80, 90, 90);
        result.Should().Be(235);
    }

    [Fact]
    public void CalculateBaseProgress_ClassJobLevelZero_SkipsModifier()
    {
        var result = FormulaCalculator.CalculateBaseProgress(3800, 130, 80, 90, 0);
        result.Should().Be(294);
    }

    [Fact]
    public void CalculateBaseProgress_TruncatesOnce()
    {
        // (3000*10/130 + 2) * 90/100 = 209.5 → 209; flooring 3000*10/130 first gives 208.
        var result = FormulaCalculator.CalculateBaseProgress(3000, 130, 90, 90, 90);
        result.Should().Be(209);
    }

    [Fact]
    public void CalculateBaseProgress_NoModifier_Truncates()
    {
        // 1000*10/130 + 2 = 78.9 → 78
        var result = FormulaCalculator.CalculateBaseProgress(1000, 130, 80, 100, 50);
        result.Should().Be(78);
    }

    [Fact]
    public void CalculateBaseQuality_CrafterEqualToClassJobLevel_AppliesModifier()
    {
        // (3600*10/115 + 35) * 70/100 = 243.6 → 243
        var result = FormulaCalculator.CalculateBaseQuality(3600, 115, 70, 90, 90);
        result.Should().Be(243);
    }

    [Fact]
    public void CalculateBaseQuality_CrafterAboveClassJobLevel_SkipsModifier()
    {
        // 3600*10/115 + 35 = 348.0 → 348
        var result = FormulaCalculator.CalculateBaseQuality(3600, 115, 70, 100, 90);
        result.Should().Be(348);
    }

    [Fact]
    public void CalculateBaseQuality_TruncatesOnce()
    {
        // (3000*10/115 + 35) * 70/100 = 207.1 → 207; flooring 3000*10/115 first gives 206.
        var result = FormulaCalculator.CalculateBaseQuality(3000, 115, 70, 90, 90);
        result.Should().Be(207);
    }

    [Fact]
    public void CalculateBaseQuality_NoModifier_Truncates()
    {
        // 1000*10/115 + 35 = 121.9 → 121
        var result = FormulaCalculator.CalculateBaseQuality(1000, 115, 70, 100, 50);
        result.Should().Be(121);
    }
}
