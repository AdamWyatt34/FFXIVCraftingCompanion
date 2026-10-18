using FFXIV.Domain.CraftingEngine.Actions;
using FFXIV.Domain.CraftingEngine.Models;

namespace FFXIV.Tests.CraftingEngine;

public class CraftingActionBaseTests
{
    private readonly CrafterStats _stats = CraftingTestHelpers.CreateDefaultStats();
    private readonly RecipeParameters _recipe = CraftingTestHelpers.CreateDefaultRecipe();

    // --- CP Cost ---

    [Fact]
    public void GetCPCost_NormalCondition_ReturnsBaseCost()
    {
        var action = new TestAction(cpCost: 18);
        var state = CraftingTestHelpers.CreateDefaultState();

        action.GetCPCost(state).Should().Be(18);
    }

    [Fact]
    public void GetCPCost_PliantCondition_EvenCost_HalvesExactly()
    {
        var action = new TestAction(cpCost: 18);
        var state = CraftingTestHelpers.CreateDefaultState() with { Condition = CraftCondition.Pliant };

        action.GetCPCost(state).Should().Be(9);
    }

    [Fact]
    public void GetCPCost_PliantCondition_OddCost_HalvesWithCeiling()
    {
        var action = new TestAction(cpCost: 7);
        var state = CraftingTestHelpers.CreateDefaultState() with { Condition = CraftCondition.Pliant };

        // ceiling(7/2) = 4
        action.GetCPCost(state).Should().Be(4);
    }

    // --- Durability Cost ---

    [Fact]
    public void GetDurabilityCost_NormalCondition_ReturnsBaseCost()
    {
        var action = new TestAction(durabilityCost: 10);
        var state = CraftingTestHelpers.CreateDefaultState();

        action.GetDurabilityCost(state).Should().Be(10);
    }

    [Fact]
    public void GetDurabilityCost_ZeroBaseCost_AlwaysReturnsZero()
    {
        var action = new TestAction(durabilityCost: 0);
        var state = CraftingTestHelpers.CreateDefaultState() with
        {
            WasteNotTurns = 4,
            Condition = CraftCondition.Sturdy,
        };

        action.GetDurabilityCost(state).Should().Be(0);
    }

    [Fact]
    public void GetDurabilityCost_TrainedPerfectionActive_ReturnsZero()
    {
        var action = new TestAction(durabilityCost: 10);
        var state = CraftingTestHelpers.CreateDefaultState() with { TrainedPerfectionActive = true };

        action.GetDurabilityCost(state).Should().Be(0);
    }

    [Fact]
    public void GetDurabilityCost_WasteNotActive_HalvesWithCeiling()
    {
        var action = new TestAction(durabilityCost: 10);
        var state = CraftingTestHelpers.CreateDefaultState() with { WasteNotTurns = 4 };

        // ceiling(10/2) = 5
        action.GetDurabilityCost(state).Should().Be(5);
    }

    [Fact]
    public void GetDurabilityCost_WasteNotIIActive_HalvesWithCeiling()
    {
        var action = new TestAction(durabilityCost: 10);
        var state = CraftingTestHelpers.CreateDefaultState() with { WasteNotIITurns = 8 };

        action.GetDurabilityCost(state).Should().Be(5);
    }

    [Fact]
    public void GetDurabilityCost_SturdyCondition_HalvesWithCeiling()
    {
        var action = new TestAction(durabilityCost: 10);
        var state = CraftingTestHelpers.CreateDefaultState() with { Condition = CraftCondition.Sturdy };

        action.GetDurabilityCost(state).Should().Be(5);
    }

    [Fact]
    public void GetDurabilityCost_WasteNotAndSturdy_StackMultiplicatively()
    {
        var action = new TestAction(durabilityCost: 10);
        var state = CraftingTestHelpers.CreateDefaultState() with
        {
            WasteNotTurns = 4,
            Condition = CraftCondition.Sturdy,
        };

        // 10 / 2 / 2 = 2.5 → ceiling = 3
        action.GetDurabilityCost(state).Should().Be(3);
    }

    [Fact]
    public void GetDurabilityCost_TrainedPerfectionOverridesAll()
    {
        var action = new TestAction(durabilityCost: 10);
        var state = CraftingTestHelpers.CreateDefaultState() with
        {
            TrainedPerfectionActive = true,
            WasteNotTurns = 4,
            Condition = CraftCondition.Sturdy,
        };

        action.GetDurabilityCost(state).Should().Be(0);
    }

    // --- Success Rate ---

    [Fact]
    public void GetSuccessRate_NormalCondition_ReturnsBaseRate()
    {
        var action = new TestAction(successRate: 80);
        var state = CraftingTestHelpers.CreateDefaultState();

        action.GetSuccessRate(state).Should().Be(80);
    }

    [Fact]
    public void GetSuccessRate_CenteredCondition_Adds25()
    {
        var action = new TestAction(successRate: 60);
        var state = CraftingTestHelpers.CreateDefaultState() with { Condition = CraftCondition.Centered };

        action.GetSuccessRate(state).Should().Be(85);
    }

    [Fact]
    public void GetSuccessRate_CenteredCondition_CapsAt100()
    {
        var action = new TestAction(successRate: 90);
        var state = CraftingTestHelpers.CreateDefaultState() with { Condition = CraftCondition.Centered };

        action.GetSuccessRate(state).Should().Be(100);
    }

    // --- CanExecute / GetFailReason ---

    [Fact]
    public void CanExecute_CraftNotInProgress_ReturnsFalse()
    {
        var action = new TestAction();
        var state = CraftingTestHelpers.CreateDefaultState() with { Status = CraftStatus.Completed };

        action.CanExecute(state, _stats, _recipe).Should().BeFalse();
    }

    [Fact]
    public void CanExecute_InsufficientLevel_ReturnsFalse()
    {
        var action = new TestAction(levelRequirement: 99);
        var stats = _stats with { Level = 50 };
        var state = CraftingTestHelpers.CreateDefaultState();

        action.CanExecute(state, stats, _recipe).Should().BeFalse();
    }

    [Fact]
    public void CanExecute_InsufficientCP_ReturnsFalse()
    {
        var action = new TestAction(cpCost: 100);
        var state = CraftingTestHelpers.CreateDefaultState() with { CP = 50 };

        action.CanExecute(state, _stats, _recipe).Should().BeFalse();
    }

    [Fact]
    public void CanExecute_AllChecksPassing_ReturnsTrue()
    {
        var action = new TestAction(cpCost: 10, levelRequirement: 1);
        var state = CraftingTestHelpers.CreateDefaultState();

        action.CanExecute(state, _stats, _recipe).Should().BeTrue();
    }

    [Fact]
    public void GetFailReason_CraftCompleted_ReturnsMessage()
    {
        var action = new TestAction();
        var state = CraftingTestHelpers.CreateDefaultState() with { Status = CraftStatus.Completed };

        action.GetFailReason(state, _stats, _recipe).Should().Be("Craft is not in progress");
    }

    [Fact]
    public void GetFailReason_InsufficientLevel_ReturnsMessage()
    {
        var action = new TestAction(levelRequirement: 99);
        var stats = _stats with { Level = 50 };
        var state = CraftingTestHelpers.CreateDefaultState();

        action.GetFailReason(state, stats, _recipe).Should().Be("Requires level 99");
    }

    [Fact]
    public void GetFailReason_InsufficientCP_ReturnsMessage()
    {
        var action = new TestAction(cpCost: 100);
        var state = CraftingTestHelpers.CreateDefaultState() with { CP = 50 };

        action.GetFailReason(state, _stats, _recipe).Should().Be("Not enough CP");
    }

    [Fact]
    public void GetFailReason_AllGood_ReturnsNull()
    {
        var action = new TestAction(cpCost: 10);
        var state = CraftingTestHelpers.CreateDefaultState();

        action.GetFailReason(state, _stats, _recipe).Should().BeNull();
    }

    // --- Execute Pipeline ---

    [Fact]
    public void Execute_DeductsCPCorrectly()
    {
        var action = new TestAction(cpCost: 32);
        var state = CraftingTestHelpers.CreateDefaultState() with { CP = 600 };

        var result = action.Execute(state, _stats, _recipe);

        result.CP.Should().Be(568);
    }

    [Fact]
    public void Execute_DeductsDurabilityCorrectly()
    {
        var action = new TestAction(durabilityCost: 10);
        var state = CraftingTestHelpers.CreateDefaultState() with { Durability = 70 };

        var result = action.Execute(state, _stats, _recipe);

        result.Durability.Should().Be(60);
    }

    [Fact]
    public void Execute_IncrementsStep()
    {
        var action = new TestAction();
        var state = CraftingTestHelpers.CreateDefaultState() with { Step = 3 };

        var result = action.Execute(state, _stats, _recipe);

        result.Step.Should().Be(4);
    }

    [Fact]
    public void Execute_ClearsFirstStepFlag()
    {
        var action = new TestAction();
        var state = CraftingTestHelpers.CreateDefaultState() with { IsFirstStep = true };

        var result = action.Execute(state, _stats, _recipe);

        result.IsFirstStep.Should().BeFalse();
    }

    [Fact]
    public void Execute_ConsumesTrainedPerfection_WhenBaseDurabilityCostPositive()
    {
        var action = new TestAction(durabilityCost: 10);
        var state = CraftingTestHelpers.CreateDefaultState() with { TrainedPerfectionActive = true };

        var result = action.Execute(state, _stats, _recipe);

        result.TrainedPerfectionActive.Should().BeFalse();
        result.Durability.Should().Be(70); // No durability lost due to TP
    }

    [Fact]
    public void Execute_DoesNotConsumeTrainedPerfection_WhenBaseDurabilityCostZero()
    {
        var action = new TestAction(durabilityCost: 0);
        var state = CraftingTestHelpers.CreateDefaultState() with { TrainedPerfectionActive = true };

        var result = action.Execute(state, _stats, _recipe);

        result.TrainedPerfectionActive.Should().BeTrue();
    }

    [Fact]
    public void Execute_TicksBuffs()
    {
        var action = new TestAction();
        var state = CraftingTestHelpers.CreateDefaultState() with
        {
            VenerationTurns = 3,
            InnovationTurns = 2,
        };

        var result = action.Execute(state, _stats, _recipe);

        result.VenerationTurns.Should().Be(2);
        result.InnovationTurns.Should().Be(1);
    }

    [Fact]
    public void Execute_ManipulationHeals_WhenActiveBeforeTick()
    {
        var action = new TestAction(durabilityCost: 10);
        var state = CraftingTestHelpers.CreateDefaultState() with
        {
            Durability = 40,
            ManipulationTurns = 3,
        };

        var result = action.Execute(state, _stats, _recipe);

        // 40 - 10 = 30, then Manipulation heals +5 = 35
        result.Durability.Should().Be(35);
        result.ManipulationTurns.Should().Be(2);
    }

    [Fact]
    public void Execute_ManipulationHeals_OnLastTurn()
    {
        var action = new TestAction(durabilityCost: 10);
        var state = CraftingTestHelpers.CreateDefaultState() with
        {
            Durability = 40,
            ManipulationTurns = 1,
        };

        var result = action.Execute(state, _stats, _recipe);

        // ManipTurns was 1 before tick (> 0), so heals
        // 40 - 10 = 30, heal +5 = 35
        result.Durability.Should().Be(35);
        result.ManipulationTurns.Should().Be(0);
    }

    [Fact]
    public void Execute_ManipulationDoesNotHeal_WhenDurabilityIsZero()
    {
        var action = new TestAction(durabilityCost: 10);
        var state = CraftingTestHelpers.CreateDefaultState() with
        {
            Durability = 10,
            ManipulationTurns = 3,
        };

        var result = action.Execute(state, _stats, _recipe);

        // 10 - 10 = 0, durability is 0 so no heal
        result.Durability.Should().Be(0);
        result.Status.Should().Be(CraftStatus.Failed);
    }

    [Fact]
    public void Execute_ManipulationCapsAtMaxDurability()
    {
        var action = new TestAction(durabilityCost: 0);
        var state = CraftingTestHelpers.CreateDefaultState() with
        {
            Durability = 70,
            MaxDurability = 70,
            ManipulationTurns = 3,
        };

        var result = action.Execute(state, _stats, _recipe);

        result.Durability.Should().Be(70); // Can't exceed max
    }

    [Fact]
    public void Execute_SetsCompleted_WhenProgressMeetsMax()
    {
        var action = new TestAction();
        var state = CraftingTestHelpers.CreateDefaultState() with
        {
            Progress = 3500,
            MaxProgress = 3500,
        };

        var result = action.Execute(state, _stats, _recipe);

        result.Status.Should().Be(CraftStatus.Completed);
    }

    [Fact]
    public void Execute_SetsFailed_WhenDurabilityReachesZero()
    {
        var action = new TestAction(durabilityCost: 10);
        var state = CraftingTestHelpers.CreateDefaultState() with { Durability = 10 };

        var result = action.Execute(state, _stats, _recipe);

        result.Status.Should().Be(CraftStatus.Failed);
    }

    [Fact]
    public void Execute_CompletedTakesPriority_OverFailed()
    {
        // Progress already at max AND durability will hit 0
        var action = new TestAction(durabilityCost: 10);
        var state = CraftingTestHelpers.CreateDefaultState() with
        {
            Progress = 3500,
            MaxProgress = 3500,
            Durability = 10,
        };

        var result = action.Execute(state, _stats, _recipe);

        result.Status.Should().Be(CraftStatus.Completed);
    }

    // --- Test Double ---

    private sealed class TestAction : CraftingActionBase
    {
        public override int Id => 999;
        public override string Name => "TestAction";
        public override string InGameName => "Test Action";
        public override int LevelRequirement { get; }
        public override ActionCategory Category => ActionCategory.Other;
        protected override int BaseCPCost { get; }
        protected override int BaseDurabilityCost { get; }
        protected override int BaseSuccessRate { get; }

        public TestAction(
            int cpCost = 0,
            int durabilityCost = 0,
            int successRate = 100,
            int levelRequirement = 1)
        {
            BaseCPCost = cpCost;
            BaseDurabilityCost = durabilityCost;
            BaseSuccessRate = successRate;
            LevelRequirement = levelRequirement;
        }

        protected override CraftingState ApplyEffect(
            CraftingState state, CrafterStats stats, RecipeParameters recipe) => state;
    }
}
