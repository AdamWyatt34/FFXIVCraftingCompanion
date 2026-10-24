using FFXIV.Domain.CraftingEngine;
using FFXIV.Domain.CraftingEngine.Actions;
using FFXIV.Domain.CraftingEngine.Models;

namespace FFXIV.Tests.CraftingEngine;

public class QualityActionTests
{
    private readonly CrafterStats _stats = CraftingTestHelpers.CreateDefaultStats();
    private readonly RecipeParameters _recipe = CraftingTestHelpers.CreateDefaultRecipe() with { ClassJobLevel = 90 };

    [Fact]
    public void ApplyEffect_CalculatesQualityFromBaseQuality()
    {
        var action = new TestQualityAction(potency: 100);
        var state = CraftingTestHelpers.CreateDefaultState() with { InnerQuietStacks = 0 };

        var result = action.Execute(state, _stats, _recipe);

        // Base: (3600*10/115 + 35) * 70/100 = 243.6 → 243
        // 243 * 100 * ((0 + 10) * 10) * 2 / 20000 = 243 (Normal counts 2 halves)
        result.Quality.Should().Be(243);
    }

    [Fact]
    public void ApplyEffect_InnerQuietIncreasesQuality()
    {
        var action = new TestQualityAction(potency: 100);
        var state = CraftingTestHelpers.CreateDefaultState() with { InnerQuietStacks = 5 };

        var result = action.Execute(state, _stats, _recipe);

        // Inner Quiet scales the gain, not Control: 243 * 100 * ((5 + 10) * 10) * 2 / 20000 = 364.5 → 364
        result.Quality.Should().Be(364);
    }

    [Fact]
    public void ApplyEffect_InnovationMultipliesPotencyBy1_5()
    {
        var action = new TestQualityAction(potency: 100);
        var state = CraftingTestHelpers.CreateDefaultState() with { InnovationTurns = 3 };

        var result = action.Execute(state, _stats, _recipe);

        // 243 * 100 * (10 * (10 + 5)) * 2 / 20000 = 364.5 → 364
        result.Quality.Should().Be(364);
    }

    [Fact]
    public void ApplyEffect_GreatStridesDoublesPotency()
    {
        var action = new TestQualityAction(potency: 100);
        var state = CraftingTestHelpers.CreateDefaultState() with { GreatStridesTurns = 3 };

        var result = action.Execute(state, _stats, _recipe);

        // 243 * 100 * (10 * (10 + 10)) * 2 / 20000 = 486
        result.Quality.Should().Be(486);
    }

    [Fact]
    public void ApplyEffect_GoodCondition_Multiplies1_5x()
    {
        var action = new TestQualityAction(potency: 100);
        var state = CraftingTestHelpers.CreateDefaultState() with { Condition = CraftCondition.Good };

        var result = action.Execute(state, _stats, _recipe);

        // Good counts 3 halves: 243 * 100 * 100 * 3 / 20000 = 364.5 → 364
        result.Quality.Should().Be(364);
    }

    [Fact]
    public void ApplyEffect_GoodCondition_TruncatesOnce()
    {
        var action = new TestQualityAction(potency: 125);
        var state = CraftingTestHelpers.CreateDefaultState() with { Condition = CraftCondition.Good };

        var result = action.Execute(state, _stats, _recipe);

        // 243 * 125 * 100 * 3 / 20000 = 455.6 → 455; truncating before the condition gives 454.
        result.Quality.Should().Be(455);
    }

    [Fact]
    public void ApplyEffect_ExcellentCondition_Multiplies4x()
    {
        var action = new TestQualityAction(potency: 100);
        var state = CraftingTestHelpers.CreateDefaultState() with { Condition = CraftCondition.Excellent };

        var result = action.Execute(state, _stats, _recipe);

        // Excellent counts 8 halves: 243 * 100 * 100 * 8 / 20000 = 972
        result.Quality.Should().Be(972);
    }

    [Fact]
    public void ApplyEffect_PoorCondition_Multiplies0_5x()
    {
        var action = new TestQualityAction(potency: 100);
        var state = CraftingTestHelpers.CreateDefaultState() with { Condition = CraftCondition.Poor };

        var result = action.Execute(state, _stats, _recipe);

        // Poor counts 1 half: 243 * 100 * 100 * 1 / 20000 = 121.5 → 121
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

    private sealed class TestQualityAction : QualityAction
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
