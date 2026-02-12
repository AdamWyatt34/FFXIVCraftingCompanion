using FFXIV.Domain.CraftingEngine;
using FFXIV.Domain.CraftingEngine.Actions;
using FFXIV.Domain.CraftingEngine.Models;

namespace FFXIV.Tests.CraftingEngine;

public class QualityActionTests
{
    private readonly CrafterStats _stats = CraftingTestHelpers.CreateDefaultStats();
    private readonly RecipeParameters _recipe = CraftingTestHelpers.CreateDefaultRecipe();

    [Fact]
    public void ApplyEffect_CalculatesQualityWithEffectiveControl()
    {
        var action = new TestQualityAction(potency: 100);
        var state = CraftingTestHelpers.CreateDefaultState() with { InnerQuietStacks = 0 };

        var result = action.Execute(state, _stats, _recipe);

        // Control=3600, IQ=0 → effectiveControl = 3600
        // Base: 3600*10/115 + 35 = 313+35 = 348
        // Modifier: 348*70/100 = 243
        // Efficiency: 100% → floor(243 * 100 / 100) = 243
        // Normal condition: ×1.0
        result.Quality.Should().Be(243);
    }

    [Fact]
    public void ApplyEffect_InnerQuietIncreasesQuality()
    {
        var action = new TestQualityAction(potency: 100);
        var state = CraftingTestHelpers.CreateDefaultState() with { InnerQuietStacks = 5 };

        var result = action.Execute(state, _stats, _recipe);

        // Control=3600, IQ=5 → effectiveControl = (int)(3600*(1+0.5)) = 5400
        // Base: 5400*10/115 + 35 = 469+35 = 504
        // Modifier: 504*70/100 = 352
        // Efficiency: 100% → 352
        result.Quality.Should().Be(352);
    }

    [Fact]
    public void ApplyEffect_InnovationAdds50ToEfficiency()
    {
        var action = new TestQualityAction(potency: 100);
        var state = CraftingTestHelpers.CreateDefaultState() with { InnovationTurns = 3 };

        var result = action.Execute(state, _stats, _recipe);

        // Base quality = 243 (same as first test)
        // Efficiency: 100 + 50 = 150% → floor(243 * 150 / 100) = floor(364.5) = 364
        result.Quality.Should().Be(364);
    }

    [Fact]
    public void ApplyEffect_GreatStridesAdds100ToEfficiency()
    {
        var action = new TestQualityAction(potency: 100);
        var state = CraftingTestHelpers.CreateDefaultState() with { GreatStridesTurns = 3 };

        var result = action.Execute(state, _stats, _recipe);

        // Efficiency: 100 + 100 = 200% → floor(243 * 200 / 100) = 486
        result.Quality.Should().Be(486);
    }

    [Fact]
    public void ApplyEffect_GoodCondition_Multiplies1_5x()
    {
        var action = new TestQualityAction(potency: 100);
        var state = CraftingTestHelpers.CreateDefaultState() with { Condition = CraftCondition.Good };

        var result = action.Execute(state, _stats, _recipe);

        // Base quality increase: 243, Good: (int)(243 * 1.5) = (int)364.5 = 364
        result.Quality.Should().Be(364);
    }

    [Fact]
    public void ApplyEffect_ExcellentCondition_Multiplies4x()
    {
        var action = new TestQualityAction(potency: 100);
        var state = CraftingTestHelpers.CreateDefaultState() with { Condition = CraftCondition.Excellent };

        var result = action.Execute(state, _stats, _recipe);

        // 243 * 4 = 972
        result.Quality.Should().Be(972);
    }

    [Fact]
    public void ApplyEffect_PoorCondition_Multiplies0_5x()
    {
        var action = new TestQualityAction(potency: 100);
        var state = CraftingTestHelpers.CreateDefaultState() with { Condition = CraftCondition.Poor };

        var result = action.Execute(state, _stats, _recipe);

        // (int)(243 * 0.5) = (int)121.5 = 121
        result.Quality.Should().Be(121);
    }

    [Fact]
    public void ApplyEffect_ConsumesGreatStrides()
    {
        var action = new TestQualityAction(potency: 100);
        var state = CraftingTestHelpers.CreateDefaultState() with { GreatStridesTurns = 3 };

        var result = action.Execute(state, _stats, _recipe);

        // GreatStrides set to 0 by ApplyEffect, then ticked → still 0
        result.GreatStridesTurns.Should().Be(0);
    }

    [Fact]
    public void ApplyEffect_IncrementsInnerQuiet()
    {
        var action = new TestQualityAction(potency: 100);
        var state = CraftingTestHelpers.CreateDefaultState() with { InnerQuietStacks = 3 };

        var result = action.Execute(state, _stats, _recipe);

        result.InnerQuietStacks.Should().Be(4);
    }

    [Fact]
    public void ApplyEffect_InnerQuiet_CappedAt10()
    {
        var action = new TestQualityAction(potency: 100);
        var state = CraftingTestHelpers.CreateDefaultState() with { InnerQuietStacks = 10 };

        var result = action.Execute(state, _stats, _recipe);

        result.InnerQuietStacks.Should().Be(10);
    }

    [Fact]
    public void ApplyEffect_Quality_CappedAtMaxQuality()
    {
        var action = new TestQualityAction(potency: 100);
        var state = CraftingTestHelpers.CreateDefaultState() with
        {
            Quality = 7100,
            MaxQuality = 7200,
            Condition = CraftCondition.Excellent,
        };

        var result = action.Execute(state, _stats, _recipe);

        // Would add 972 to 7100 = 8072, but capped at 7200
        result.Quality.Should().Be(7200);
    }

    // --- Test Double ---

    private class TestQualityAction : QualityAction
    {
        public override int Id => 1001;
        public override string Name => "TestQuality";
        public override string InGameName => "Test Quality";
        public override int LevelRequirement => 1;
        protected override int BaseCPCost => 0;
        protected override int Potency { get; }

        public TestQualityAction(int potency = 100)
        {
            Potency = potency;
        }
    }
}
