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

        // Duration 4, set in ApplyEffect, then ticked by WithBuffTick → 3
        result.WasteNotTurns.Should().Be(3);
    }

    // --- Waste Not II ---

    [Fact]
    public void WasteNotII_AppliesBuff()
    {
        var action = new WasteNotII();
        var state = CraftingTestHelpers.CreateDefaultState();

        var result = action.Execute(state, _stats, _recipe);

        // Duration 8, ticked → 7
        result.WasteNotIITurns.Should().Be(7);
    }

    // --- Veneration ---

    [Fact]
    public void Veneration_AppliesBuff()
    {
        var action = new Veneration();
        var state = CraftingTestHelpers.CreateDefaultState();

        var result = action.Execute(state, _stats, _recipe);

        // Duration 4, ticked → 3
        result.VenerationTurns.Should().Be(3);
    }

    // --- Innovation ---

    [Fact]
    public void Innovation_AppliesBuff()
    {
        var action = new Innovation();
        var state = CraftingTestHelpers.CreateDefaultState();

        var result = action.Execute(state, _stats, _recipe);

        // Duration 4, ticked → 3
        result.InnovationTurns.Should().Be(3);
    }

    // --- Great Strides ---

    [Fact]
    public void GreatStrides_AppliesBuff()
    {
        var action = new GreatStrides();
        var state = CraftingTestHelpers.CreateDefaultState();

        var result = action.Execute(state, _stats, _recipe);

        // Duration 3, ticked → 2
        result.GreatStridesTurns.Should().Be(2);
    }

    // --- Manipulation ---

    [Fact]
    public void Manipulation_AppliesBuff()
    {
        var action = new Manipulation();
        var state = CraftingTestHelpers.CreateDefaultState();

        var result = action.Execute(state, _stats, _recipe);

        // Duration 8, ticked → 7
        result.ManipulationTurns.Should().Be(7);
    }

    // --- Final Appraisal ---

    [Fact]
    public void FinalAppraisal_AppliesBuff()
    {
        var action = new FinalAppraisal();
        var state = CraftingTestHelpers.CreateDefaultState();

        var result = action.Execute(state, _stats, _recipe);

        // Duration 5, ticked → 4
        result.FinalAppraisalTurns.Should().Be(4);
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
    public void HeartAndSoul_ExpertRecipesOnly()
    {
        var action = new HeartAndSoul();
        var state = CraftingTestHelpers.CreateDefaultState();
        var normalRecipe = _recipe with { IsExpert = false };

        action.CanExecute(state, _stats, normalRecipe).Should().BeFalse();
        action.GetFailReason(state, _stats, normalRecipe).Should().Contain("expert");
    }

    [Fact]
    public void HeartAndSoul_CanExecuteOnExpertRecipe()
    {
        var action = new HeartAndSoul();
        var state = CraftingTestHelpers.CreateDefaultState();
        var expertRecipe = _recipe with { IsExpert = true };

        action.CanExecute(state, _stats, expertRecipe).Should().BeTrue();
    }

    [Fact]
    public void HeartAndSoul_OncePerCraft()
    {
        var action = new HeartAndSoul();
        var state = CraftingTestHelpers.CreateDefaultState() with { HeartAndSoulUsed = true };
        var expertRecipe = _recipe with { IsExpert = true };

        action.CanExecute(state, _stats, expertRecipe).Should().BeFalse();
        action.GetFailReason(state, _stats, expertRecipe).Should().Contain("once");
    }

    [Fact]
    public void HeartAndSoul_SetsActiveAndUsedFlags()
    {
        var action = new HeartAndSoul();
        var state = CraftingTestHelpers.CreateDefaultState();
        var expertRecipe = _recipe with { IsExpert = true };

        var result = action.Execute(state, _stats, expertRecipe);

        result.HeartAndSoulActive.Should().BeTrue();
        result.HeartAndSoulUsed.Should().BeTrue();
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
        var stats = _stats with { Level = 96 };

        action.CanExecute(state, stats, _recipe).Should().BeFalse();
    }

    [Fact]
    public void QuickInnovation_Grants1TurnInnovation()
    {
        var action = new QuickInnovation();
        var state = CraftingTestHelpers.CreateDefaultState();
        var stats = _stats with { Level = 96 };

        var result = action.Execute(state, stats, _recipe);

        // Innovation set to 1 in ApplyEffect, then ticked → 0
        result.InnovationTurns.Should().Be(0);
        result.QuickInnovationUsed.Should().BeTrue();
    }

    [Fact]
    public void QuickInnovation_Costs0CP()
    {
        var action = new QuickInnovation();
        var state = CraftingTestHelpers.CreateDefaultState();

        action.GetCPCost(state).Should().Be(0);
    }

    // --- Primed Condition ---

    [Fact]
    public void Buff_WithPrimedCondition_ExtendsDuration2Turns()
    {
        var action = new Veneration();
        var state = CraftingTestHelpers.CreateDefaultState() with { Condition = CraftCondition.Primed };

        var result = action.Execute(state, _stats, _recipe);

        // Duration = 4 + 2 (Primed) = 6, ticked → 5
        result.VenerationTurns.Should().Be(5);
    }
}
