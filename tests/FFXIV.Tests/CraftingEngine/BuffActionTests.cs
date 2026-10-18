using FFXIV.Domain.CraftingEngine.Actions;
using FFXIV.Domain.CraftingEngine.Models;

namespace FFXIV.Tests.CraftingEngine;

public class BuffActionTests
{
    private readonly CrafterStats _stats = CraftingTestHelpers.CreateDefaultStats();
    private readonly RecipeParameters _recipe = CraftingTestHelpers.CreateDefaultRecipe();

    [Fact]
    public void GetEffectiveDuration_NormalCondition_ReturnsBaseDuration()
    {
        var action = new TestBuffAction(buffDuration: 4);
        var state = CraftingTestHelpers.CreateDefaultState();

        var result = action.Execute(state, _stats, _recipe);

        // BuffDuration = 4, Normal condition → 4
        result.VenerationTurns.Should().Be(3); // 4 applied, then WithBuffTick → 3
    }

    [Fact]
    public void GetEffectiveDuration_PrimedCondition_AddsTwo()
    {
        var action = new TestBuffAction(buffDuration: 4);
        var state = CraftingTestHelpers.CreateDefaultState() with { Condition = CraftCondition.Primed };

        var result = action.Execute(state, _stats, _recipe);

        // BuffDuration = 4 + 2 = 6, then WithBuffTick → 5
        result.VenerationTurns.Should().Be(5);
    }

    [Fact]
    public void BaseDurabilityCost_IsZero()
    {
        var action = new TestBuffAction(buffDuration: 4);
        var state = CraftingTestHelpers.CreateDefaultState();

        action.GetDurabilityCost(state).Should().Be(0);
    }

    [Fact]
    public void GetDurabilityCost_AlwaysZero_RegardlessOfConditions()
    {
        var action = new TestBuffAction(buffDuration: 4);
        var state = CraftingTestHelpers.CreateDefaultState() with
        {
            WasteNotTurns = 4,
            Condition = CraftCondition.Sturdy,
        };

        action.GetDurabilityCost(state).Should().Be(0);
    }

    // --- Test Double ---

    private sealed class TestBuffAction : BuffAction
    {
        public override int Id => 1002;
        public override string Name => "TestBuff";
        public override string InGameName => "Test Buff";
        public override int LevelRequirement => 1;
        protected override int BaseCPCost => 0;
        protected override int BuffDuration { get; }

        public TestBuffAction(int buffDuration = 4)
        {
            BuffDuration = buffDuration;
        }

        protected override CraftingState ApplyEffect(
            CraftingState state, CrafterStats stats, RecipeParameters recipe)
        {
            // Simulates setting a buff like Veneration
            return state with { VenerationTurns = GetEffectiveDuration(state) };
        }
    }
}
