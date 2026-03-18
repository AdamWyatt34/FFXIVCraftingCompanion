using FFXIV.Domain.CraftingEngine.Actions.Quality;
using FFXIV.Domain.CraftingEngine.Models;
using FluentAssertions;

namespace FFXIV.Tests.CraftingEngine.Actions;

public class QualityActionsTests
{
    private readonly CrafterStats _stats = CraftingTestHelpers.CreateDefaultStats();
    private readonly RecipeParameters _recipe = CraftingTestHelpers.CreateDefaultRecipe();

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
    public void StandardTouch_Execute_SetsComboForAdvancedTouch()
    {
        var action = new StandardTouch();
        var state = CraftingTestHelpers.CreateDefaultState();

        var result = action.Execute(state, _stats, _recipe);

        result.ComboAction.Should().Be(StandardTouch.ActionId);
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

    // --- Focused Touch ---

    [Fact]
    public void FocusedTouch_WithoutObserve_Returns50()
    {
        var action = new FocusedTouch();
        var state = CraftingTestHelpers.CreateDefaultState();

        action.GetSuccessRate(state).Should().Be(50);
    }

    [Fact]
    public void FocusedTouch_AfterObserve_Returns100()
    {
        var action = new FocusedTouch();
        var state = CraftingTestHelpers.CreateDefaultState() with { ObserveActive = true };

        action.GetSuccessRate(state).Should().Be(100);
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
    public void Reflect_Grants1IQFromAction()
    {
        var action = new Reflect();
        var state = CraftingTestHelpers.CreateDefaultState();

        var result = action.Execute(state, _stats, _recipe);

        // GetInnerQuietGain returns 1 (base quality action adds 1 IQ)
        // High potency (300) gives significant quality
        result.InnerQuietStacks.Should().Be(1);
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
}
