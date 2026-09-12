using FFXIV.Domain.CraftingEngine.Actions.Buffs;
using FFXIV.Domain.CraftingEngine.Models;
using FluentAssertions;

namespace FFXIV.Tests.CraftingEngine.Actions;

public class BuffActionsTests
{
    private readonly CrafterStats _stats = CraftingTestHelpers.CreateDefaultStats();
    private readonly RecipeParameters _recipe = CraftingTestHelpers.CreateDefaultRecipe();

    // --- Inner Quiet ---

    [Fact]
    public void InnerQuiet_IsNoOp_InDawntrail()
    {
        var action = new InnerQuiet();
        var state = CraftingTestHelpers.CreateDefaultState();

        var result = action.Execute(state, _stats, _recipe);

        // Should only change Step, CP (18 deducted), and IsFirstStep
        result.InnerQuietStacks.Should().Be(0);
    }

    // --- Waste Not ---

    [Fact]
    public void WasteNot_AppliesBuff()
    {
        var action = new WasteNot();
        var state = CraftingTestHelpers.CreateDefaultState();

        var result = action.Execute(state, _stats, _recipe);

        result.WasteNotTurns.Should().Be(4);
    }

    // --- Waste Not II ---

    [Fact]
    public void WasteNotII_AppliesBuff()
    {
        var action = new WasteNotII();
        var state = CraftingTestHelpers.CreateDefaultState();

        var result = action.Execute(state, _stats, _recipe);

        result.WasteNotIITurns.Should().Be(8);
    }

    // --- Veneration ---

    [Fact]
    public void Veneration_AppliesBuff()
    {
        var action = new Veneration();
        var state = CraftingTestHelpers.CreateDefaultState();

        var result = action.Execute(state, _stats, _recipe);

        result.VenerationTurns.Should().Be(4);
    }

    // --- Innovation ---

    [Fact]
    public void Innovation_AppliesBuff()
    {
        var action = new Innovation();
        var state = CraftingTestHelpers.CreateDefaultState();

        var result = action.Execute(state, _stats, _recipe);

        result.InnovationTurns.Should().Be(4);
    }

    // --- Great Strides ---

    [Fact]
    public void GreatStrides_AppliesBuff()
    {
        var action = new GreatStrides();
        var state = CraftingTestHelpers.CreateDefaultState();

        var result = action.Execute(state, _stats, _recipe);

        result.GreatStridesTurns.Should().Be(3);
    }

    // --- Manipulation ---

    [Fact]
    public void Manipulation_AppliesBuff()
    {
        var action = new Manipulation();
        var state = CraftingTestHelpers.CreateDefaultState();

        var result = action.Execute(state, _stats, _recipe);

        result.ManipulationTurns.Should().Be(8);
    }

    // --- Final Appraisal ---

    [Fact]
    public void FinalAppraisal_AppliesBuff()
    {
        var action = new FinalAppraisal();
        var state = CraftingTestHelpers.CreateDefaultState();

        var result = action.Execute(state, _stats, _recipe);

        result.FinalAppraisalTurns.Should().Be(5);
    }

    [Fact]
    public void FinalAppraisal_CostsOnly1CP()
    {
        var action = new FinalAppraisal();
        var state = CraftingTestHelpers.CreateDefaultState();

        action.GetCPCost(state).Should().Be(1);
    }

    // --- Observe ---

    [Fact]
    public void Observe_SetsObserveActive()
    {
        var action = new Observe();
        var state = CraftingTestHelpers.CreateDefaultState();

        var result = action.Execute(state, _stats, _recipe);

        result.ObserveActive.Should().BeTrue();
    }

    [Fact]
    public void Observe_Costs7CP()
    {
        var action = new Observe();
        var state = CraftingTestHelpers.CreateDefaultState();

        action.GetCPCost(state).Should().Be(7);
    }

    // --- Heart and Soul ---

    [Fact]
    public void HeartAndSoul_SpecialistOnly()
    {
        var action = new HeartAndSoul();
        var state = CraftingTestHelpers.CreateDefaultState();
        var nonSpecialist = _stats with { IsSpecialist = false };

        action.CanExecute(state, nonSpecialist, _recipe).Should().BeFalse();
        action.GetFailReason(state, nonSpecialist, _recipe).Should().Contain("specialist");
    }

    [Fact]
    public void HeartAndSoul_CanExecuteAsSpecialistOnAnyRecipe()
    {
        var action = new HeartAndSoul();
        var state = CraftingTestHelpers.CreateDefaultState();
        var specialist = _stats with { IsSpecialist = true };

        action.CanExecute(state, specialist, _recipe with { IsExpert = false }).Should().BeTrue();
        action.CanExecute(state, specialist, _recipe with { IsExpert = true }).Should().BeTrue();
    }

    [Fact]
    public void HeartAndSoul_OncePerCraft()
    {
        var action = new HeartAndSoul();
        var state = CraftingTestHelpers.CreateDefaultState() with { HeartAndSoulUsed = true };
        var specialist = _stats with { IsSpecialist = true };

        action.CanExecute(state, specialist, _recipe).Should().BeFalse();
        action.GetFailReason(state, specialist, _recipe).Should().Contain("once");
    }

    [Fact]
    public void HeartAndSoul_SetsActiveAndUsedFlags_WithoutConsumingAStep()
    {
        var action = new HeartAndSoul();
        var state = CraftingTestHelpers.CreateDefaultState() with { InnovationTurns = 2 };
        var specialist = _stats with { IsSpecialist = true };

        var result = action.Execute(state, specialist, _recipe);

        result.HeartAndSoulActive.Should().BeTrue();
        result.HeartAndSoulUsed.Should().BeTrue();
        result.Step.Should().Be(0);
        result.IsFirstStep.Should().BeTrue();
        result.InnovationTurns.Should().Be(2);
    }

    // --- Trained Perfection ---

    [Fact]
    public void TrainedPerfection_OncePerCraft()
    {
        var action = new TrainedPerfection();
        var state = CraftingTestHelpers.CreateDefaultState() with { TrainedPerfectionUsed = true };
        var stats = _stats with { Level = 100 };

        action.CanExecute(state, stats, _recipe).Should().BeFalse();
    }

    [Fact]
    public void TrainedPerfection_SetsActiveAndUsedFlags()
    {
        var action = new TrainedPerfection();
        var state = CraftingTestHelpers.CreateDefaultState();
        var stats = _stats with { Level = 100 };

        var result = action.Execute(state, stats, _recipe);

        result.TrainedPerfectionActive.Should().BeTrue();
        result.TrainedPerfectionUsed.Should().BeTrue();
    }

    // --- Quick Innovation ---

    [Fact]
    public void QuickInnovation_OncePerCraft()
    {
        var action = new QuickInnovation();
        var state = CraftingTestHelpers.CreateDefaultState() with { QuickInnovationUsed = true };
        var stats = _stats with { Level = 96, IsSpecialist = true };

        action.CanExecute(state, stats, _recipe).Should().BeFalse();
    }

    [Fact]
    public void QuickInnovation_Grants1TurnInnovation_WithoutConsumingAStep()
    {
        var action = new QuickInnovation();
        var state = CraftingTestHelpers.CreateDefaultState() with { VenerationTurns = 3 };
        var stats = _stats with { Level = 96, IsSpecialist = true };

        var result = action.Execute(state, stats, _recipe);

        result.InnovationTurns.Should().Be(1);
        result.QuickInnovationUsed.Should().BeTrue();
        result.Step.Should().Be(0);
        result.VenerationTurns.Should().Be(3);
    }

    [Fact]
    public void QuickInnovation_SpecialistOnly()
    {
        var action = new QuickInnovation();
        var state = CraftingTestHelpers.CreateDefaultState();
        var stats = _stats with { Level = 96, IsSpecialist = false };

        action.CanExecute(state, stats, _recipe).Should().BeFalse();
        action.GetFailReason(state, stats, _recipe).Should().Contain("specialist");
    }

    [Fact]
    public void QuickInnovation_Costs0CP()
    {
        var action = new QuickInnovation();
        var state = CraftingTestHelpers.CreateDefaultState();

        action.GetCPCost(state).Should().Be(0);
    }

    [Fact]
    public void QuickInnovation_BlockedWhileInnovationActive()
    {
        var action = new QuickInnovation();
        var state = CraftingTestHelpers.CreateDefaultState() with { InnovationTurns = 3 };
        var stats = _stats with { Level = 96, IsSpecialist = true };

        action.CanExecute(state, stats, _recipe).Should().BeFalse();
        action.GetFailReason(state, stats, _recipe).Should().Contain("Innovation");
    }

    [Fact]
    public void QuickInnovation_AllowedWhenInnovationNotActive()
    {
        var action = new QuickInnovation();
        var state = CraftingTestHelpers.CreateDefaultState() with { InnovationTurns = 0 };
        var stats = _stats with { Level = 96, IsSpecialist = true };

        action.CanExecute(state, stats, _recipe).Should().BeTrue();
    }

    // --- Primed Condition ---

    [Fact]
    public void Buff_WithPrimedCondition_ExtendsDuration2Turns()
    {
        var action = new Veneration();
        var state = CraftingTestHelpers.CreateDefaultState() with { Condition = CraftCondition.Primed };

        var result = action.Execute(state, _stats, _recipe);

        result.VenerationTurns.Should().Be(6);
    }
}
