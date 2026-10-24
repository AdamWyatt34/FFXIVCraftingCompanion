using FFXIV.Domain.CraftingEngine;
using FFXIV.Domain.CraftingEngine.Models;

namespace FFXIV.Tests.CraftingEngine;

// Every expected number is computed with Raphael's integer arithmetic (Teamcraft agrees), never read
// from this engine's output: the parity goldens only pin the engine against itself.
public class ReferenceParityTests
{
    private readonly CraftingSimulator _simulator = new();

    private static readonly RecipeParameters Rlvl690 = new()
    {
        RecipeLevel = 690,
        ClassJobLevel = 100,
        Difficulty = 6600,
        Quality = 12000,
        Durability = 80,
        ProgressDivider = 170,
        ProgressModifier = 90,
        QualityDivider = 150,
        QualityModifier = 75,
    };

    private static readonly RecipeParameters Rlvl640FourStar = new()
    {
        RecipeLevel = 640,
        ClassJobLevel = 90,
        Difficulty = 4400,
        Quality = 9000,
        Durability = 70,
        ProgressDivider = 130,
        ProgressModifier = 80,
        QualityDivider = 115,
        QualityModifier = 70,
    };

    private static readonly RecipeParameters Rlvl133 = new()
    {
        RecipeLevel = 133,
        ClassJobLevel = 54,
        Difficulty = 5000,
        Quality = 10000,
        Durability = 80,
        ProgressDivider = 64,
        ProgressModifier = 100,
        QualityDivider = 44,
        QualityModifier = 100,
    };

    private static readonly CrafterStats Lv100 = new() { Level = 100, Craftsmanship = 4000, Control = 4000, CP = 900 };

    // Raphael raphael-data/src/lib.rs:145-149,175-192.
    [Fact]
    public void Base_progress_and_quality_on_rlvl_690()
    {
        FormulaCalculator.CalculateBaseProgress(4000, 170, 90, 100, 100).Should().Be(213);
        FormulaCalculator.CalculateBaseQuality(4000, 150, 75, 100, 100).Should().Be(226);
    }

    // Raphael raphael-sim/src/effects.rs:70-75, actions.rs:28-36; Teamcraft progress-action.ts:26-36.
    [Fact]
    public void Veneration_multiplies_basic_synthesis()
    {
        var result = Simulate(Rlvl690, Lv100, "Veneration", "BasicSynthesis");

        result.Steps[1].ProgressDelta.Should().Be(383);
    }

    // Raphael raphael-sim/src/effects.rs:70-75: Muscle Memory and Veneration sum to 25 tenths, not 2 x 1.5.
    [Fact]
    public void Muscle_memory_and_veneration_sum_into_one_multiplier()
    {
        var result = Simulate(Rlvl690, Lv100, "MuscleMemory", "Veneration", "Groundwork");

        result.Steps[0].ProgressDelta.Should().Be(639);
        result.Steps[2].ProgressDelta.Should().Be(1917);
    }

    // Raphael raphael-sim/src/effects.rs:76-80, actions.rs:38-52; Teamcraft quality-action.ts:79-94.
    [Fact]
    public void Inner_quiet_innovation_and_great_strides_multiply_quality()
    {
        var result = Simulate(Rlvl690, Lv100,
            "Reflect", "Innovation", "GreatStrides", "BasicTouch", "GreatStrides", "ByregotsBlessing");

        result.Steps.Select(s => s.QualityDelta).Should().Equal(678, 0, 0, 678, 0, 1175);
        result.FinalState.Quality.Should().Be(2531);
    }

    // Raphael raphael-sim/src/actions.rs:30-41; Teamcraft quality-action.ts:65-77.
    [Theory]
    [InlineData(CraftCondition.Normal, 226)]
    [InlineData(CraftCondition.Good, 339)]
    [InlineData(CraftCondition.Excellent, 904)]
    [InlineData(CraftCondition.Poor, 113)]
    public void Condition_scales_basic_touch(CraftCondition condition, int expected)
    {
        var state = _simulator.CreateInitialState(Rlvl690, Lv100) with { Condition = condition };

        _simulator.ExecuteAction(state, Id("BasicTouch"), Lv100, Rlvl690).QualityDelta.Should().Be(expected);
    }

    // Raphael raphael-sim/src/effects.rs:76-80 with the condition halves inside the numerator (actions.rs:30-41).
    [Theory]
    [InlineData(CraftCondition.Normal, 813)]
    [InlineData(CraftCondition.Good, 1220)]
    public void Preparatory_touch_at_inner_quiet_8(CraftCondition condition, int expected)
    {
        var recipe = Rlvl690 with { Durability = 120 };
        var prefix = Simulate(recipe, Lv100, "Reflect", "PreparatoryTouch", "PreparatoryTouch", "PreparatoryTouch");
        prefix.FinalState.InnerQuietStacks.Should().Be(8);

        var state = prefix.FinalState with { Condition = condition };

        _simulator.ExecuteAction(state, Id("PreparatoryTouch"), Lv100, recipe).QualityDelta.Should().Be(expected);
    }

    // Raphael raphael-data/src/lib.rs:147-149 (job_level, not rlvl); Teamcraft crafting-action.ts getBaseProgression.
    [Theory]
    [InlineData(100, 370, 382)]
    [InlineData(90, 296, 267)]
    public void Level_modifier_follows_the_class_job_level(int level, int expectedProgress, int expectedQuality)
    {
        var stats = Lv100 with { Level = level };

        var result = Simulate(Rlvl640FourStar, stats, "BasicSynthesis", "BasicTouch");

        result.Steps[0].ProgressDelta.Should().Be(expectedProgress);
        result.Steps[1].QualityDelta.Should().Be(expectedQuality);
    }

    // Raphael raphael-data/src/lib.rs:145-149; Teamcraft crafting-action.ts getBaseProgression: one truncation.
    [Fact]
    public void Base_progress_truncates_once()
    {
        var recipe = new RecipeParameters
        {
            RecipeLevel = 590,
            ClassJobLevel = 90,
            Difficulty = 5000,
            Quality = 10000,
            Durability = 80,
            ProgressDivider = 130,
            ProgressModifier = 90,
            QualityDivider = 115,
            QualityModifier = 75,
        };
        var stats = new CrafterStats { Level = 90, Craftsmanship = 3000, Control = 3500, CP = 300 };

        FormulaCalculator.CalculateBaseProgress(3000, 130, 90, 90, 90).Should().Be(209);
        Simulate(recipe, stats, "BasicSynthesis").Steps[0].ProgressDelta.Should().Be(250);
    }

    // Raphael raphael-sim/src/state.rs:120; Teamcraft quality-action.ts:96.
    [Theory]
    [InlineData(8, 0)]
    [InlineData(10, 0)]
    [InlineData(11, 1)]
    public void Inner_quiet_is_gained_from_level_11(int level, int expectedStacks)
    {
        var stats = new CrafterStats { Level = level, Craftsmanship = 529, Control = 500, CP = 300 };

        Simulate(Rlvl133, stats, "BasicTouch").FinalState.InnerQuietStacks.Should().Be(expectedStacks);
    }

    // Raphael raphael-sim/src/actions.rs:158-172,236-280,553-590; Teamcraft quality/advanced-touch.ts hasCombo.
    [Theory]
    [InlineData(new[] { "StandardTouch", "AdvancedTouch" }, new[] { 32, 46 })]
    [InlineData(new[] { "Observe", "AdvancedTouch" }, new[] { 7, 18 })]
    [InlineData(new[] { "BasicTouch", "StandardTouch", "AdvancedTouch" }, new[] { 18, 18, 18 })]
    [InlineData(new[] { "Observe", "StandardTouch", "AdvancedTouch" }, new[] { 7, 32, 46 })]
    public void Advanced_touch_is_discounted_after_a_comboed_standard_touch_or_observe(string[] actions, int[] expectedCosts)
    {
        var result = Simulate(Rlvl690, Lv100, actions);

        result.Steps.Select(s => -s.CPDelta).Should().Equal(expectedCosts);
    }

    // Raphael raphael-sim/src/actions.rs RapidSynthesis progress_modifier; Teamcraft progression/rapid-synthesis.ts getPotency.
    [Theory]
    [InlineData(50, 210)]
    [InlineData(63, 420)]
    public void Rapid_synthesis_potency_doubles_at_level_63(int level, int expected)
    {
        var stats = new CrafterStats { Level = level, Craftsmanship = 529, Control = 500, CP = 300 };

        Simulate(Rlvl133, stats, "RapidSynthesis").Steps[0].ProgressDelta.Should().Be(expected);
    }

    // Raphael raphael-sim/src/actions.rs WasteNot/WasteNot2 .with_waste_not(0); Teamcraft buff/waste-not.ts:35-37,
    // waste-not-ii.ts:35-37.
    [Fact]
    public void Waste_not_replaces_waste_not_ii()
    {
        var recipe = new RecipeParameters
        {
            RecipeLevel = 690,
            ClassJobLevel = 100,
            Difficulty = 99999,
            Quality = 99999,
            Durability = 100,
            ProgressDivider = 170,
            ProgressModifier = 100,
            QualityDivider = 150,
            QualityModifier = 100,
        };
        var stats = Lv100 with { CP = 2000 };

        var result = Simulate(recipe, stats, ["WasteNotII", "WasteNot", .. Enumerable.Repeat("BasicTouch", 8)]);

        result.FinalState.Durability.Should().Be(40);
    }

    private SimulationResult Simulate(RecipeParameters recipe, CrafterStats stats, params string[] actions) =>
        _simulator.Simulate(recipe, stats, actions.Select(Id).ToList());

    private static int Id(string name) =>
        CraftingActionRegistry.GetByName(name)?.Id ?? throw new InvalidOperationException($"Unknown action '{name}'");
}
