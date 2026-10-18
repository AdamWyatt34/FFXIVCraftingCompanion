using FFXIV.Domain.CraftingEngine;
using FFXIV.Domain.CraftingEngine.Actions;
using FFXIV.Domain.CraftingEngine.Models;

namespace FFXIV.Tests.CraftingEngine;

public class ProgressActionTests
{
    private readonly CrafterStats _stats = CraftingTestHelpers.CreateDefaultStats();
    private readonly RecipeParameters _recipe = CraftingTestHelpers.CreateDefaultRecipe();

    [Fact]
    public void ApplyEffect_CalculatesProgressCorrectly()
    {
        var action = new TestProgressAction(potency: 120);
        var state = CraftingTestHelpers.CreateDefaultState();

        var result = action.Execute(state, _stats, _recipe);

        // Base: 3800*10/130 + 2 = 294, modifier: 294*80/100 = 235
        // Efficiency: 120% → floor(235 * 120 / 100) = floor(282.0) = 282
        result.Progress.Should().Be(282);
    }

    [Fact]
    public void ApplyEffect_VenerationAdds50ToEfficiency()
    {
        var action = new TestProgressAction(potency: 120);
        var state = CraftingTestHelpers.CreateDefaultState() with { VenerationTurns = 3 };

        var result = action.Execute(state, _stats, _recipe);

        // Efficiency: 120 + 50 = 170% → floor(235 * 170 / 100) = floor(399.5) = 399
        result.Progress.Should().Be(399);
    }

    [Fact]
    public void ApplyEffect_MuscleMemoryAdds100ToEfficiency()
    {
        var action = new TestProgressAction(potency: 120);
        var state = CraftingTestHelpers.CreateDefaultState() with { MuscleMemoryTurns = 5 };

        var result = action.Execute(state, _stats, _recipe);

        // Efficiency: 120 + 100 = 220% → floor(235 * 220 / 100) = floor(517.0) = 517
        result.Progress.Should().Be(517);
    }

    [Fact]
    public void ApplyEffect_VenerationAndMuscleMemory_StackAdditively()
    {
        var action = new TestProgressAction(potency: 120);
        var state = CraftingTestHelpers.CreateDefaultState() with
        {
            VenerationTurns = 3,
            MuscleMemoryTurns = 5,
        };

        var result = action.Execute(state, _stats, _recipe);

        // Efficiency: 120 + 50 + 100 = 270% → floor(235 * 270 / 100) = floor(634.5) = 634
        result.Progress.Should().Be(634);
    }

    [Fact]
    public void ApplyEffect_ConsumesMuscleMemory()
    {
        var action = new TestProgressAction(potency: 120);
        var state = CraftingTestHelpers.CreateDefaultState() with { MuscleMemoryTurns = 5 };

        var result = action.Execute(state, _stats, _recipe);

        // MuscleMemory set to 0 by ApplyEffect, then ticked by WithBuffTick → still 0
        result.MuscleMemoryTurns.Should().Be(0);
    }

    [Fact]
    public void ApplyEffect_FinalAppraisal_CapsProgressAtMaxMinusOne()
    {
        var action = new TestProgressAction(potency: 300);
        var state = CraftingTestHelpers.CreateDefaultState() with
        {
            MaxProgress = 500,
            FinalAppraisalTurns = 3,
        };

        var result = action.Execute(state, _stats, _recipe);

        // Progress would exceed 500, Final Appraisal caps at 499
        result.Progress.Should().Be(499);
        result.Status.Should().Be(CraftStatus.InProgress);
    }

    [Fact]
    public void ApplyEffect_FinalAppraisal_ConsumedWhenTriggered()
    {
        var action = new TestProgressAction(potency: 300);
        var state = CraftingTestHelpers.CreateDefaultState() with
        {
            MaxProgress = 500,
            FinalAppraisalTurns = 3,
        };

        var result = action.Execute(state, _stats, _recipe);

        // FinalAppraisal set to 0 by ApplyEffect, then ticked → still 0
        result.FinalAppraisalTurns.Should().Be(0);
    }

    [Fact]
    public void ApplyEffect_FinalAppraisal_NotConsumedWhenProgressBelowMax()
    {
        var action = new TestProgressAction(potency: 10);
        var state = CraftingTestHelpers.CreateDefaultState() with
        {
            MaxProgress = 50000,
            FinalAppraisalTurns = 3,
        };

        var result = action.Execute(state, _stats, _recipe);

        // Progress is well below max, Final Appraisal not triggered
        // FinalAppraisalTurns: 3, then WithBuffTick → 2
        result.FinalAppraisalTurns.Should().Be(2);
    }

    // --- Test Double ---

    private sealed class TestProgressAction : ProgressAction
    {
        public override int Id => 1000;
        public override string Name => "TestProgress";
        public override string InGameName => "Test Progress";
        public override int LevelRequirement => 1;
        protected override int BaseCPCost => 0;
        private readonly int _potency;
        protected override int GetPotency(CraftingState state, CrafterStats stats) => _potency;

        public TestProgressAction(int potency = 120)
        {
            _potency = potency;
        }
    }
}
