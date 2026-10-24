using FFXIV.Domain.CraftingEngine;
using FFXIV.Domain.CraftingEngine.Actions.Buffs;
using FFXIV.Domain.CraftingEngine.Actions.Quality;
using FFXIV.Domain.CraftingEngine.Models;
using FluentAssertions;

namespace FFXIV.Tests.CraftingEngine.Actions;

public class QualityActionsTests
{
    private readonly CrafterStats _stats = CraftingTestHelpers.CreateDefaultStats();
    private readonly RecipeParameters _recipe = CraftingTestHelpers.CreateDefaultRecipe();
    private readonly CraftingSimulator _simulator = new();

    // Expected numbers on this recipe come from Raphael's simulator (base quality 226 at Lv100 4000 control),
    // never from this engine's output.
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

    private static readonly CrafterStats Lv100 = new() { Level = 100, Craftsmanship = 4000, Control = 4000, CP = 900 };

    // --- Basic Touch ---

    [Fact]
    public void BasicTouch_Execute_AddsQuality()
    {
        var action = new BasicTouch();
        var state = CraftingTestHelpers.CreateDefaultState();

        var result = action.Execute(state, _stats, _recipe);

        result.Quality.Should().BeGreaterThan(0);
    }

    [Fact]
    public void BasicTouch_Execute_SetsComboAction()
    {
        var action = new BasicTouch();
        var state = CraftingTestHelpers.CreateDefaultState();

        var result = action.Execute(state, _stats, _recipe);

        result.ComboAction.Should().Be(BasicTouch.ActionId);
    }

    // --- Standard Touch Combo ---

    [Fact]
    public void StandardTouch_AfterBasicTouch_CostsOnly18CP()
    {
        var action = new StandardTouch();
        var state = CraftingTestHelpers.CreateDefaultState() with
        {
            ComboAction = BasicTouch.ActionId,
        };

        action.GetCPCost(state).Should().Be(18);
    }

    [Fact]
    public void StandardTouch_WithoutCombo_CostsFull32CP()
    {
        var action = new StandardTouch();
        var state = CraftingTestHelpers.CreateDefaultState();

        action.GetCPCost(state).Should().Be(32);
    }

    [Fact]
    public void StandardTouch_ComboWithPliant_HalvesReducedCost()
    {
        var action = new StandardTouch();
        var state = CraftingTestHelpers.CreateDefaultState() with
        {
            ComboAction = BasicTouch.ActionId,
            Condition = CraftCondition.Pliant,
        };

        action.GetCPCost(state).Should().Be(9); // ceil(18/2)
    }

    [Fact]
    public void StandardTouch_AfterBasicTouch_SetsComboForAdvancedTouch()
    {
        var action = new StandardTouch();
        var state = CraftingTestHelpers.CreateDefaultState() with { ComboAction = BasicTouch.ActionId };

        var result = action.Execute(state, _stats, _recipe);

        result.ComboAction.Should().Be(StandardTouch.ActionId);
    }

    [Fact]
    public void StandardTouch_WithoutCombo_DoesNotSetComboForAdvancedTouch()
    {
        var action = new StandardTouch();
        var state = CraftingTestHelpers.CreateDefaultState();

        var result = action.Execute(state, _stats, _recipe);

        result.ComboAction.Should().Be(0);
    }

    // --- Advanced Touch Combo ---

    [Fact]
    public void AdvancedTouch_AfterStandardTouch_CostsOnly18CP()
    {
        var action = new AdvancedTouch();
        var state = CraftingTestHelpers.CreateDefaultState() with
        {
            ComboAction = StandardTouch.ActionId,
        };

        action.GetCPCost(state).Should().Be(18);
    }

    [Fact]
    public void AdvancedTouch_WithoutCombo_CostsFull46CP()
    {
        var action = new AdvancedTouch();
        var state = CraftingTestHelpers.CreateDefaultState();

        action.GetCPCost(state).Should().Be(46);
    }

    // --- Full Combo Chain ---

    [Fact]
    public void ComboChain_BasicToStandardToAdvanced_CorrectCPCosts()
    {
        var basic = new BasicTouch();
        var standard = new StandardTouch();
        var advanced = new AdvancedTouch();
        var state = CraftingTestHelpers.CreateDefaultState();

        // Step 1: Basic Touch (18 CP)
        var afterBasic = basic.Execute(state, _stats, _recipe);
        afterBasic.ComboAction.Should().Be(BasicTouch.ActionId);
        var basicCost = basic.GetCPCost(state);
        basicCost.Should().Be(18);

        // Step 2: Standard Touch (18 CP combo)
        standard.GetCPCost(afterBasic).Should().Be(18);
        var afterStandard = standard.Execute(afterBasic, _stats, _recipe);
        afterStandard.ComboAction.Should().Be(StandardTouch.ActionId);

        // Step 3: Advanced Touch (18 CP combo)
        advanced.GetCPCost(afterStandard).Should().Be(18);

        // Total combo CP: 18 + 18 + 18 = 54 instead of 18 + 32 + 46 = 96
    }

    [Fact]
    public void ComboChain_StandardTouchWithoutBasicTouch_AdvancedTouchCostsFull46CP()
    {
        var standard = new StandardTouch();
        var advanced = new AdvancedTouch();

        var afterStandard = standard.Execute(CraftingTestHelpers.CreateDefaultState(), _stats, _recipe);

        advanced.GetCPCost(afterStandard).Should().Be(46);
    }

    // --- Hasty Touch ---

    [Fact]
    public void HastyTouch_GetSuccessRate_Returns60()
    {
        var action = new HastyTouch();
        var state = CraftingTestHelpers.CreateDefaultState();

        action.GetSuccessRate(state).Should().Be(60);
    }

    [Fact]
    public void HastyTouch_Costs0CP()
    {
        var action = new HastyTouch();
        var state = CraftingTestHelpers.CreateDefaultState();

        action.GetCPCost(state).Should().Be(0);
    }

    [Theory]
    [InlineData(95, false)]
    [InlineData(96, true)]
    [InlineData(100, true)]
    public void HastyTouch_GrantsExpedienceFromLevel96(int level, bool expected)
    {
        var result = Simulate(Rlvl690, Lv100 with { Level = level }, "HastyTouch");

        result.FinalState.ExpedienceActive.Should().Be(expected);
    }

    // Teamcraft simulation.ts runAction: execute, which grants Expedience, runs only on a successful roll.
    [Fact]
    public void HastyTouch_FailedRoll_GrantsNoExpedience()
    {
        var state = _simulator.CreateInitialState(Rlvl690, Lv100);

        new HastyTouch().Execute(state, Lv100, Rlvl690, forceSuccess: false).ExpedienceActive.Should().BeFalse();
    }

    // Raphael raphael-sim/src/actions.rs:1120-1123: the refused Hasty Touch leaves the state, Expedience included,
    // so the Daring Touch after it gains what it gains straight after the first Hasty Touch.
    [Fact]
    public void HastyTouch_UnderExpedience_IsBlocked()
    {
        var result = Simulate(Rlvl690, Lv100, "HastyTouch", "HastyTouch", "DaringTouch");

        result.Steps[1].ActionSucceeded.Should().BeFalse();
        result.Steps[1].FailReason.Should().Be("Hasty Touch becomes Daring Touch under Expedience");
        result.Steps[2].ActionSucceeded.Should().BeTrue();
        result.Steps.Select(s => s.QualityDelta).Should().Equal(226, 0, 372);
        result.FinalState.ExpedienceActive.Should().BeFalse();
    }

    [Fact]
    public void HastyTouch_BelowLevel96_CanRepeat()
    {
        var result = Simulate(Rlvl690, Lv100 with { Level = 95 }, "HastyTouch", "HastyTouch");

        result.Steps.Should().OnlyContain(s => s.ActionSucceeded);
    }

    // Raphael raphael-sim/src/actions.rs:392-438, replayed through its simulator; Teamcraft quality/precise-touch.ts.
    [Theory]
    [InlineData(CraftCondition.Good, 508)]
    [InlineData(CraftCondition.Excellent, 1356)]
    public void PreciseTouch_OnGoodOrExcellent_AddsQualityAndTwoInnerQuiet(CraftCondition condition, int expectedQuality)
    {
        var state = _simulator.CreateInitialState(Rlvl690, Lv100) with { Condition = condition };

        var step = _simulator.ExecuteAction(state, PreciseTouch.ActionId, Lv100, Rlvl690);

        step.QualityDelta.Should().Be(expectedQuality);
        step.CPDelta.Should().Be(-18);
        step.DurabilityDelta.Should().Be(-10);
        step.StateAfter.InnerQuietStacks.Should().Be(2);
    }

    [Theory]
    [InlineData(CraftCondition.Normal)]
    [InlineData(CraftCondition.Poor)]
    [InlineData(CraftCondition.Centered)]
    public void PreciseTouch_WithoutGoodOrHeartAndSoul_IsBlocked(CraftCondition condition)
    {
        var state = _simulator.CreateInitialState(Rlvl690, Lv100) with { Condition = condition };

        new PreciseTouch().GetFailReason(state, Lv100, Rlvl690).Should().Contain("Good or Excellent");
    }

    // Raphael raphael-sim/src/actions.rs:392-438 transform: Heart and Soul is spent only when the condition did not
    // already allow the action.
    [Theory]
    [InlineData(CraftCondition.Normal, 339, false)]
    [InlineData(CraftCondition.Good, 508, true)]
    public void PreciseTouch_UnderHeartAndSoul(CraftCondition condition, int expectedQuality, bool heartAndSoulKept)
    {
        var specialist = Lv100 with { IsSpecialist = true };
        var state = _simulator.CreateInitialState(Rlvl690, specialist) with
        {
            Condition = condition,
            HeartAndSoulActive = true,
            HeartAndSoulUsed = true,
        };

        var step = _simulator.ExecuteAction(state, PreciseTouch.ActionId, specialist, Rlvl690);

        step.QualityDelta.Should().Be(expectedQuality);
        step.StateAfter.HeartAndSoulActive.Should().Be(heartAndSoulKept);
    }

    [Fact]
    public void PreciseTouch_RequiresLevel53()
    {
        var state = _simulator.CreateInitialState(Rlvl690, Lv100) with { Condition = CraftCondition.Good };

        new PreciseTouch().GetFailReason(state, Lv100 with { Level = 52 }, Rlvl690).Should().Be("Requires level 53");
    }

    // Raphael raphael-sim/src/actions.rs:923-966, replayed through its simulator; Teamcraft quality/refined-touch.ts.
    [Fact]
    public void RefinedTouch_AfterBasicTouch_GainsAnExtraInnerQuiet()
    {
        var result = Simulate(Rlvl690, Lv100, "BasicTouch", "RefinedTouch");

        result.Steps.Select(s => s.QualityDelta).Should().Equal(226, 248);
        result.Steps.Select(s => -s.CPDelta).Should().Equal(18, 24);
        result.FinalState.InnerQuietStacks.Should().Be(3);
    }

    // Teamcraft quality/refined-touch.ts: usable outside the combo, which only adds the extra stack (Raphael's solver
    // forbids that use). 310 is from Raphael's simulator; 271 is its quality formula at Inner Quiet 2.
    [Fact]
    public void RefinedTouch_AfterStandardTouch_GainsOneInnerQuiet()
    {
        var result = Simulate(Rlvl690, Lv100, "BasicTouch", "StandardTouch", "RefinedTouch");

        result.Steps.Should().OnlyContain(s => s.ActionSucceeded);
        result.Steps.Select(s => s.QualityDelta).Should().Equal(226, 310, 271);
        result.FinalState.InnerQuietStacks.Should().Be(3);
    }

    [Fact]
    public void RefinedTouch_RequiresLevel92()
    {
        var state = _simulator.CreateInitialState(Rlvl690, Lv100) with { ComboAction = BasicTouch.ActionId };

        new RefinedTouch().GetFailReason(state, Lv100 with { Level = 91 }, Rlvl690).Should().Be("Requires level 92");
    }

    // Raphael raphael-sim/src/actions.rs:1142-1177, replayed through its simulator; Teamcraft quality/daring-touch.ts.
    [Theory]
    [InlineData(96)]
    [InlineData(100)]
    public void DaringTouch_AfterHastyTouch_AddsQuality(int level)
    {
        var result = Simulate(Rlvl690, Lv100 with { Level = level }, "HastyTouch", "DaringTouch");

        result.Steps.Should().OnlyContain(s => s.ActionSucceeded);
        result.Steps.Select(s => s.QualityDelta).Should().Equal(226, 372);
        result.Steps.Select(s => -s.CPDelta).Should().Equal(0, 0);
        result.FinalState.Durability.Should().Be(60);
        result.FinalState.InnerQuietStacks.Should().Be(2);
        result.FinalState.ExpedienceActive.Should().BeFalse();
    }

    [Fact]
    public void DaringTouch_BelowLevel96_IsBlocked()
    {
        var stats = Lv100 with { Level = 95 };

        var result = Simulate(Rlvl690, stats, "HastyTouch", "DaringTouch");

        result.Steps[1].ActionSucceeded.Should().BeFalse();
        result.Steps[1].FailReason.Should().Contain("Expedience");
        var underExpedience = _simulator.CreateInitialState(Rlvl690, stats) with { ExpedienceActive = true };
        new DaringTouch().GetFailReason(underExpedience, stats, Rlvl690).Should().Be("Requires level 96");
    }

    // Raphael raphael-sim/src/effects.rs tick_down: any step ends Expedience, whatever the action.
    [Fact]
    public void DaringTouch_AfterAnInterveningStep_IsBlocked()
    {
        var result = Simulate(Rlvl690, Lv100, "HastyTouch", "BasicTouch", "DaringTouch");

        result.Steps[2].ActionSucceeded.Should().BeFalse();
        result.Steps[2].FailReason.Should().Contain("Expedience");
    }

    // Raphael's simulator (no tick without a step; Quick Innovation's Innovation gives 559) and Teamcraft
    // heart-and-soul.ts skipsBuffTicks.
    [Theory]
    [InlineData("HeartAndSoul", 372)]
    [InlineData("QuickInnovation", 559)]
    public void Expedience_SurvivesStepFreeAbilities(string stepFree, int expectedQuality)
    {
        var specialist = Lv100 with { IsSpecialist = true };

        var result = Simulate(Rlvl690, specialist, "HastyTouch", stepFree, "DaringTouch");

        result.Steps.Should().OnlyContain(s => s.ActionSucceeded);
        result.Steps[2].QualityDelta.Should().Be(expectedQuality);
    }

    // Teamcraft simulation.ts: a failed roll still ticks the buffs, so it spends Expedience.
    [Fact]
    public void DaringTouch_FailedRoll_SpendsExpedience()
    {
        var action = new DaringTouch();
        var state = _simulator.CreateInitialState(Rlvl690, Lv100) with { ExpedienceActive = true };

        action.GetCPCost(state).Should().Be(0);
        action.GetSuccessRate(state).Should().Be(60);
        action.Execute(state, Lv100, Rlvl690, forceSuccess: false).ExpedienceActive.Should().BeFalse();
    }

    // --- Focused Touch ---

    [Fact]
    public void FocusedTouch_WithoutObserve_Returns50()
    {
        var action = new FocusedTouch();
        var state = CraftingTestHelpers.CreateDefaultState();

        action.GetSuccessRate(state).Should().Be(50);
    }

    [Fact]
    public void FocusedTouch_AfterObserve_StaysAt50()
    {
        var action = new FocusedTouch();
        var state = new Observe().Execute(CraftingTestHelpers.CreateDefaultState(), _stats, _recipe);

        action.GetSuccessRate(state).Should().Be(50);
    }

    // --- Preparatory Touch ---

    [Fact]
    public void PreparatoryTouch_Uses20Durability()
    {
        var action = new PreparatoryTouch();
        var state = CraftingTestHelpers.CreateDefaultState();

        action.GetDurabilityCost(state).Should().Be(20);
    }

    [Fact]
    public void PreparatoryTouch_Grants2IQStacks()
    {
        var action = new PreparatoryTouch();
        var state = CraftingTestHelpers.CreateDefaultState() with { InnerQuietStacks = 3 };

        var result = action.Execute(state, _stats, _recipe);

        result.InnerQuietStacks.Should().Be(5); // 3 + 2
    }

    // --- Prudent Touch ---

    [Fact]
    public void PrudentTouch_BlockedByWasteNot()
    {
        var action = new PrudentTouch();
        var state = CraftingTestHelpers.CreateDefaultState() with { WasteNotTurns = 3 };

        action.CanExecute(state, _stats, _recipe).Should().BeFalse();
        action.GetFailReason(state, _stats, _recipe).Should().Contain("Waste Not");
    }

    [Fact]
    public void PrudentTouch_Uses5Durability()
    {
        var action = new PrudentTouch();
        var state = CraftingTestHelpers.CreateDefaultState();

        action.GetDurabilityCost(state).Should().Be(5);
    }

    // --- Reflect ---

    [Fact]
    public void Reflect_FirstStepOnly()
    {
        var action = new Reflect();
        var state = CraftingTestHelpers.CreateDefaultState() with { IsFirstStep = false };

        action.CanExecute(state, _stats, _recipe).Should().BeFalse();
    }

    [Fact]
    public void Reflect_CanExecuteOnFirstStep()
    {
        var action = new Reflect();
        var state = CraftingTestHelpers.CreateDefaultState();

        action.CanExecute(state, _stats, _recipe).Should().BeTrue();
    }

    [Fact]
    public void Reflect_GrantsTwoInnerQuietStacks()
    {
        var action = new Reflect();
        var state = CraftingTestHelpers.CreateDefaultState();

        var result = action.Execute(state, _stats, _recipe);

        result.InnerQuietStacks.Should().Be(2);
        result.Quality.Should().BeGreaterThan(0);
    }

    // --- Byregot's Blessing ---

    [Fact]
    public void ByregotsBlessing_RequiresIQStacks()
    {
        var action = new ByregotsBlessing();
        var state = CraftingTestHelpers.CreateDefaultState() with { InnerQuietStacks = 0 };

        action.CanExecute(state, _stats, _recipe).Should().BeFalse();
    }

    [Fact]
    public void ByregotsBlessing_ConsumesAllIQ()
    {
        var action = new ByregotsBlessing();
        var state = CraftingTestHelpers.CreateDefaultState() with { InnerQuietStacks = 5 };

        var result = action.Execute(state, _stats, _recipe);

        result.InnerQuietStacks.Should().Be(0);
    }

    [Fact]
    public void ByregotsBlessing_PotencyScalesWithIQ()
    {
        var action = new ByregotsBlessing();
        var lowIQ = CraftingTestHelpers.CreateDefaultState() with { InnerQuietStacks = 1 };
        var highIQ = CraftingTestHelpers.CreateDefaultState() with { InnerQuietStacks = 10 };

        var lowResult = action.Execute(lowIQ, _stats, _recipe);
        var highResult = action.Execute(highIQ, _stats, _recipe);

        // 10 IQ (potency 300) should produce much more quality than 1 IQ (potency 120)
        highResult.Quality.Should().BeGreaterThan(lowResult.Quality);
    }

    [Fact]
    public void ByregotsBlessing_At10IQ_Has300Potency()
    {
        // Potency = 100 + (10 * 20) = 300
        // At 10 IQ, effective control = 3600 * (1 + 0.1 * 10) = 7200
        // This should produce significantly more quality than BasicTouch (100 potency, 0 IQ)
        var blessing = new ByregotsBlessing();
        var basic = new BasicTouch();

        var state10 = CraftingTestHelpers.CreateDefaultState() with { InnerQuietStacks = 10 };
        var state0 = CraftingTestHelpers.CreateDefaultState();

        var blessingResult = blessing.Execute(state10, _stats, _recipe);
        var basicResult = basic.Execute(state0, _stats, _recipe);

        // With 300 potency and 2x control, Byregot's should be much higher
        blessingResult.Quality.Should().BeGreaterThan(basicResult.Quality * 4);
    }

    // --- Trained Finesse ---

    [Fact]
    public void TrainedFinesse_Requires10IQ()
    {
        var action = new TrainedFinesse();
        var state = CraftingTestHelpers.CreateDefaultState() with { InnerQuietStacks = 9 };

        action.CanExecute(state, _stats, _recipe).Should().BeFalse();
    }

    [Fact]
    public void TrainedFinesse_CanExecuteAt10IQ()
    {
        var action = new TrainedFinesse();
        var state = CraftingTestHelpers.CreateDefaultState() with { InnerQuietStacks = 10 };

        action.CanExecute(state, _stats, _recipe).Should().BeTrue();
    }

    [Fact]
    public void TrainedFinesse_Uses0Durability()
    {
        var action = new TrainedFinesse();
        var state = CraftingTestHelpers.CreateDefaultState() with { InnerQuietStacks = 10 };

        action.GetDurabilityCost(state).Should().Be(0);

        var result = action.Execute(state, _stats, _recipe);
        result.Durability.Should().Be(state.Durability); // No durability loss
    }

    // --- Trained Eye ---

    [Fact]
    public void TrainedEye_FirstStepOnly()
    {
        var action = new TrainedEye();
        var state = CraftingTestHelpers.CreateDefaultState() with { IsFirstStep = false };
        var recipe = _recipe with { ClassJobLevel = 50 };

        action.CanExecute(state, _stats, recipe).Should().BeFalse();
    }

    [Fact]
    public void TrainedEye_RequiresLevelAdvantage()
    {
        var action = new TrainedEye();
        var state = CraftingTestHelpers.CreateDefaultState();
        // Stats level is 90, recipe ClassJobLevel 85 → needs 95, can't use
        var recipe = _recipe with { ClassJobLevel = 85 };

        action.CanExecute(state, _stats, recipe).Should().BeFalse();
        action.GetFailReason(state, _stats, recipe).Should().Contain("level");
    }

    [Fact]
    public void TrainedEye_CanExecuteWithSufficientLevel()
    {
        var action = new TrainedEye();
        var state = CraftingTestHelpers.CreateDefaultState();
        // Stats level 90, ClassJobLevel 80 → needs 90, exactly meets
        var recipe = _recipe with { ClassJobLevel = 80 };

        action.CanExecute(state, _stats, recipe).Should().BeTrue();
    }

    [Fact]
    public void TrainedEye_SetsQualityTo100Percent()
    {
        var action = new TrainedEye();
        var state = CraftingTestHelpers.CreateDefaultState();
        var recipe = _recipe with { ClassJobLevel = 50 };

        var result = action.Execute(state, _stats, recipe);

        result.Quality.Should().Be(state.MaxQuality);
    }

    [Fact]
    public void TrainedEye_Uses0Durability()
    {
        var action = new TrainedEye();
        var state = CraftingTestHelpers.CreateDefaultState();
        var recipe = _recipe with { ClassJobLevel = 50 };

        action.GetDurabilityCost(state).Should().Be(0);
    }

    [Fact]
    public void TrainedEye_BlockedByExpertRecipe()
    {
        var action = new TrainedEye();
        var state = CraftingTestHelpers.CreateDefaultState();
        var recipe = _recipe with { ClassJobLevel = 50, IsExpert = true };

        action.CanExecute(state, _stats, recipe).Should().BeFalse();
        action.GetFailReason(state, _stats, recipe).Should().Contain("expert");
    }

    [Fact]
    public void TrainedEye_AllowedOnNonExpertRecipe()
    {
        var action = new TrainedEye();
        var state = CraftingTestHelpers.CreateDefaultState();
        var recipe = _recipe with { ClassJobLevel = 50, IsExpert = false };

        action.CanExecute(state, _stats, recipe).Should().BeTrue();
    }

    [Fact]
    public void TrainedEye_BlockedWhenClassJobLevelIsZero()
    {
        var action = new TrainedEye();
        var state = CraftingTestHelpers.CreateDefaultState();
        var recipe = _recipe with { ClassJobLevel = 0 };

        action.CanExecute(state, _stats, recipe).Should().BeFalse();
        action.GetFailReason(state, _stats, recipe).Should().Contain("unavailable");
    }

    private SimulationResult Simulate(RecipeParameters recipe, CrafterStats stats, params string[] actions) =>
        _simulator.Simulate(recipe, stats, actions.Select(Id).ToList());

    private static int Id(string name) =>
        CraftingActionRegistry.GetByName(name)?.Id ?? throw new InvalidOperationException($"Unknown action '{name}'");
}
