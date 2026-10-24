using FFXIV.Domain.CraftingEngine;
using FFXIV.Domain.CraftingEngine.Actions;
using FFXIV.Domain.CraftingEngine.Models;

namespace FFXIV.Tests.CraftingEngine;

public class ProgressActionTests
{
    private readonly CrafterStats _stats = CraftingTestHelpers.CreateDefaultStats();
    private readonly RecipeParameters _recipe = CraftingTestHelpers.CreateDefaultRecipe() with { ClassJobLevel = 90 };

    [Fact]
    public void ApplyEffect_CalculatesProgressCorrectly()
    {
        var action = new TestProgressAction(potency: 120);
        var state = CraftingTestHelpers.CreateDefaultState();

        var result = action.Execute(state, _stats, _recipe);

        // Base: (3800*10/130 + 2) * 80/100 = 235.4 → 235 (level 90 <= class job level 90)
        // 235 * 120 * 10 / 1000 = 282
        result.Progress.Should().Be(282);
    }

    [Fact]
    public void ApplyEffect_VenerationMultipliesPotencyBy1_5()
    {
        var action = new TestProgressAction(potency: 120);
        var state = CraftingTestHelpers.CreateDefaultState() with { VenerationTurns = 3 };

        var result = action.Execute(state, _stats, _recipe);

        // 235 * 120 * (10 + 5) / 1000 = 423
        result.Progress.Should().Be(423);
    }

    [Fact]
    public void ApplyEffect_MuscleMemoryDoublesPotency()
    {
        var action = new TestProgressAction(potency: 120);
        var state = CraftingTestHelpers.CreateDefaultState() with { MuscleMemoryTurns = 5 };

        var result = action.Execute(state, _stats, _recipe);

        // 235 * 120 * (10 + 10) / 1000 = 564
        result.Progress.Should().Be(564);
    }

    [Fact]
    public void ApplyEffect_VenerationAndMuscleMemory_SumIntoOneMultiplier()
    {
        var action = new TestProgressAction(potency: 120);
        var state = CraftingTestHelpers.CreateDefaultState() with
        {
            VenerationTurns = 3,
            MuscleMemoryTurns = 5,
        };

        var result = action.Execute(state, _stats, _recipe);

        // 235 * 120 * (10 + 10 + 5) / 1000 = 705
        result.Progress.Should().Be(705);
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
