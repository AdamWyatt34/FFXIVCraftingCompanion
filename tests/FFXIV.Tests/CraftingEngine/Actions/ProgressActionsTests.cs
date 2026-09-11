using FFXIV.Domain.CraftingEngine.Actions.Progress;
using FFXIV.Domain.CraftingEngine.Models;
using FluentAssertions;

namespace FFXIV.Tests.CraftingEngine.Actions;

public class ProgressActionsTests
{
    private readonly CrafterStats _stats = CraftingTestHelpers.CreateDefaultStats();
    private readonly RecipeParameters _recipe = CraftingTestHelpers.CreateDefaultRecipe();

    [Fact]
    public void BasicSynthesis_Execute_CalculatesProgress()
    {
        var action = new BasicSynthesis();
        var state = CraftingTestHelpers.CreateDefaultState();

        var result = action.Execute(state, _stats, _recipe);

        result.Progress.Should().BeGreaterThan(0);
        action.LevelRequirement.Should().Be(1);
        action.GetCPCost(state).Should().Be(0);
    }

    [Fact]
    public void CarefulSynthesis_Execute_HigherPotencyThanBasic()
    {
        var basic = new BasicSynthesis();
        var careful = new CarefulSynthesis();
        var state = CraftingTestHelpers.CreateDefaultState();

        var basicResult = basic.Execute(state, _stats, _recipe);
        var carefulResult = careful.Execute(state, _stats, _recipe);

        carefulResult.Progress.Should().BeGreaterThan(basicResult.Progress);
        careful.GetCPCost(state).Should().Be(7);
    }

    [Fact]
    public void RapidSynthesis_GetSuccessRate_Returns50()
    {
        var action = new RapidSynthesis();
        var state = CraftingTestHelpers.CreateDefaultState();

        action.GetSuccessRate(state).Should().Be(50);
    }

    [Fact]
    public void RapidSynthesis_GetSuccessRate_WithCentered_Returns75()
    {
        var action = new RapidSynthesis();
        var state = CraftingTestHelpers.CreateDefaultState() with { Condition = CraftCondition.Centered };

        action.GetSuccessRate(state).Should().Be(75);
    }

    [Fact]
    public void FocusedSynthesis_WithoutObserve_Returns50()
    {
        var action = new FocusedSynthesis();
        var state = CraftingTestHelpers.CreateDefaultState();

        action.GetSuccessRate(state).Should().Be(50);
    }

    [Fact]
    public void FocusedSynthesis_AfterObserve_Returns100()
    {
        var action = new FocusedSynthesis();
        var state = CraftingTestHelpers.CreateDefaultState() with { ObserveActive = true };

        action.GetSuccessRate(state).Should().Be(100);
    }

    [Fact]
    public void Groundwork_HighDurability_UsesFull300Potency()
    {
        var action = new Groundwork();
        var basic = new BasicSynthesis(); // 120 potency for comparison
        var state = CraftingTestHelpers.CreateDefaultState(); // 70 dur, cost = 20

        var result = action.Execute(state, _stats, _recipe);
        var basicResult = basic.Execute(state, _stats, _recipe);

        // Groundwork 300 potency should produce 2.5x the progress of BasicSynthesis 120 potency
        result.Progress.Should().BeGreaterThan(basicResult.Progress * 2);
    }

    [Fact]
    public void Groundwork_LowDurability_UsesHalved150Potency()
    {
        var action = new Groundwork();
        // With WasteNot, effective dur cost = ceil(20/2) = 10. Durability 5 < 10.
        var state = CraftingTestHelpers.CreateDefaultState() with
        {
            Durability = 5,
            WasteNotTurns = 3,
        };

        var fullState = CraftingTestHelpers.CreateDefaultState() with { WasteNotTurns = 3 };
        var fullResult = action.Execute(fullState, _stats, _recipe);
        var halfResult = action.Execute(state, _stats, _recipe);

        halfResult.Progress.Should().BeLessThan(fullResult.Progress);
    }

    [Fact]
    public void Groundwork_Uses20Durability()
    {
        var action = new Groundwork();
        var state = CraftingTestHelpers.CreateDefaultState();

        action.GetDurabilityCost(state).Should().Be(20);
    }

    [Fact]
    public void IntensiveSynthesis_RequiresGoodCondition()
    {
        var action = new IntensiveSynthesis();
        var state = CraftingTestHelpers.CreateDefaultState();

        action.CanExecute(state, _stats, _recipe).Should().BeFalse();
        action.GetFailReason(state, _stats, _recipe).Should().Contain("Good or Excellent");
    }

    [Fact]
    public void IntensiveSynthesis_GoodCondition_CanExecute()
    {
        var action = new IntensiveSynthesis();
        var state = CraftingTestHelpers.CreateDefaultState() with { Condition = CraftCondition.Good };

        action.CanExecute(state, _stats, _recipe).Should().BeTrue();
    }

    [Fact]
    public void IntensiveSynthesis_HeartAndSoul_CanExecute()
    {
        var action = new IntensiveSynthesis();
        var state = CraftingTestHelpers.CreateDefaultState() with { HeartAndSoulActive = true };

        action.CanExecute(state, _stats, _recipe).Should().BeTrue();
    }

    [Fact]
    public void IntensiveSynthesis_HeartAndSoul_ConsumedWhenUsedAsSubstitute()
    {
        var action = new IntensiveSynthesis();
        var state = CraftingTestHelpers.CreateDefaultState() with { HeartAndSoulActive = true };

        var result = action.Execute(state, _stats, _recipe);

        result.HeartAndSoulActive.Should().BeFalse();
    }

    [Fact]
    public void IntensiveSynthesis_GoodCondition_DoesNotConsumeHeartAndSoul()
    {
        var action = new IntensiveSynthesis();
        var state = CraftingTestHelpers.CreateDefaultState() with
        {
            Condition = CraftCondition.Good,
            HeartAndSoulActive = true,
        };

        var result = action.Execute(state, _stats, _recipe);

        result.HeartAndSoulActive.Should().BeTrue();
    }

    [Fact]
    public void PrudentSynthesis_BlockedByWasteNot()
    {
        var action = new PrudentSynthesis();
        var state = CraftingTestHelpers.CreateDefaultState() with { WasteNotTurns = 3 };

        action.CanExecute(state, _stats, _recipe).Should().BeFalse();
        action.GetFailReason(state, _stats, _recipe).Should().Contain("Waste Not");
    }

    [Fact]
    public void PrudentSynthesis_BlockedByWasteNotII()
    {
        var action = new PrudentSynthesis();
        var state = CraftingTestHelpers.CreateDefaultState() with { WasteNotIITurns = 5 };

        action.CanExecute(state, _stats, _recipe).Should().BeFalse();
    }

    [Fact]
    public void PrudentSynthesis_Uses5Durability()
    {
        var action = new PrudentSynthesis();
        var state = CraftingTestHelpers.CreateDefaultState();

        action.GetDurabilityCost(state).Should().Be(5);
    }

    [Fact]
    public void MuscleMemory_FirstStepOnly()
    {
        var action = new MuscleMemory();
        var state = CraftingTestHelpers.CreateDefaultState() with { IsFirstStep = false };

        action.CanExecute(state, _stats, _recipe).Should().BeFalse();
        action.GetFailReason(state, _stats, _recipe).Should().Contain("first action");
    }

    [Fact]
    public void MuscleMemory_GrantsMuscleMemoryBuff()
    {
        var action = new MuscleMemory();
        var state = CraftingTestHelpers.CreateDefaultState();

        var result = action.Execute(state, _stats, _recipe);

        result.MuscleMemoryTurns.Should().Be(5);
    }

    [Fact]
    public void MuscleMemory_CanExecuteOnFirstStep()
    {
        var action = new MuscleMemory();
        var state = CraftingTestHelpers.CreateDefaultState();

        action.CanExecute(state, _stats, _recipe).Should().BeTrue();
    }

    [Fact]
    public void DelicateSynthesis_AddsBothProgressAndQuality()
    {
        var action = new DelicateSynthesis();
        var state = CraftingTestHelpers.CreateDefaultState();

        var result = action.Execute(state, _stats, _recipe);

        result.Progress.Should().BeGreaterThan(0);
        result.Quality.Should().BeGreaterThan(0);
    }

    [Fact]
    public void DelicateSynthesis_IncrementsIQ()
    {
        var action = new DelicateSynthesis();
        var state = CraftingTestHelpers.CreateDefaultState() with { InnerQuietStacks = 3 };

        var result = action.Execute(state, _stats, _recipe);

        result.InnerQuietStacks.Should().Be(4);
    }

    [Fact]
    public void DelicateSynthesis_ClearsGreatStrides()
    {
        var action = new DelicateSynthesis();
        var state = CraftingTestHelpers.CreateDefaultState() with { GreatStridesTurns = 3 };

        var result = action.Execute(state, _stats, _recipe);

        // GreatStrides set to 0 in ApplyEffect, then WithBuffTick has no effect (already 0)
        result.GreatStridesTurns.Should().Be(0);
    }

    [Fact]
    public void DelicateSynthesis_HandlesFinalAppraisal()
    {
        var action = new DelicateSynthesis();
        // Set progress close to max so Delicate Synthesis would complete it
        var state = CraftingTestHelpers.CreateDefaultState() with
        {
            Progress = 3400,
            FinalAppraisalTurns = 3,
        };

        var result = action.Execute(state, _stats, _recipe);

        result.Progress.Should().Be(3499); // MaxProgress - 1
        result.FinalAppraisalTurns.Should().Be(0); // Consumed (not just ticked)
    }
}
