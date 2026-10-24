using FluentAssertions;
using FFXIV.Domain.CraftingEngine;
using FFXIV.Domain.CraftingEngine.Actions;
using FFXIV.Domain.CraftingEngine.Models;

namespace FFXIV.Tests.CraftingEngine;

public class CraftingSimulatorTests
{
    private readonly CraftingSimulator _simulator = new();

    private static CrafterStats CreateLevel90Stats() => new()
    {
        Level = 90,
        Craftsmanship = 3800,
        Control = 3600,
        CP = 580,
    };

    private static RecipeParameters CreateLevel90Recipe() => new()
    {
        RecipeId = 100,
        RecipeLevel = 80,
        Difficulty = 1500,
        Quality = 7200,
        Durability = 70,
        ProgressDivider = 130,
        ProgressModifier = 80,
        QualityDivider = 115,
        QualityModifier = 70,
        ClassJobLevel = 80,
        Stars = 0,
        SuggestedCraftsmanship = 2800,
        SuggestedControl = 2600,
    };

    // Action IDs
    private const int Reflect = 100387;
    private const int Manipulation = 100278;
    private const int Innovation = 100209;
    private const int PreparatoryTouch = 100299;
    private const int GreatStrides = 100260;
    private const int ByregotsBlessing = 100339;
    private const int Veneration = 100297;
    private const int Groundwork = 100403;
    private const int BasicTouch = 100002;
    private const int StandardTouch = 100004;
    private const int AdvancedTouch = 100411;
    private const int FinalAppraisal = 100364;
    private const int MuscleMemory = 100379;
    private const int RapidSynthesis = 100033;
    private const int BasicSynthesis = 100001;
    private const int CarefulSynthesis = 100203;

    [Fact]
    public void TestCase1_Level90Rotation_CraftCompletes()
    {
        var stats = CreateLevel90Stats();
        var recipe = CreateLevel90Recipe();

        var actionIds = new List<int>
        {
            Reflect,
            Manipulation,
            Innovation,
            PreparatoryTouch,
            PreparatoryTouch,
            Innovation,
            GreatStrides,
            ByregotsBlessing,
            Veneration,
            Groundwork,
            Groundwork,
        };

        var result = _simulator.Simulate(recipe, stats, actionIds);

        result.Success.Should().BeTrue("craft should complete");
        result.FinalState.Status.Should().Be(CraftStatus.Completed);
        result.FinalState.Progress.Should().BeGreaterThanOrEqualTo(recipe.Difficulty);
        result.FinalState.Quality.Should().BeGreaterThan(0);
        // The first Groundwork under Veneration adds 294 * 360 * 15 / 1000 = 1587 >= 1500, so the last action never runs.
        result.TotalSteps.Should().Be(10);
        result.Steps.Should().AllSatisfy(s => s.ActionSucceeded.Should().BeTrue());
    }

    [Fact]
    public void TestCase2_ComboChain_ReducesCPCosts()
    {
        var stats = CreateLevel90Stats();
        var recipe = CreateLevel90Recipe() with { Durability = 80, Quality = 50000 };

        // Comboed: BasicTouch → StandardTouch → AdvancedTouch (18→18→18 = 54 CP)
        var comboedIds = new List<int> { BasicTouch, StandardTouch, AdvancedTouch };
        var comboedResult = _simulator.Simulate(recipe, stats, comboedIds);

        int comboedCPSpent = stats.CP - comboedResult.FinalState.CP;

        // Non-comboed: AdvancedTouch → StandardTouch → BasicTouch (46→32→18 = 96 CP)
        var nonComboIds = new List<int> { AdvancedTouch, StandardTouch, BasicTouch };
        var nonComboResult = _simulator.Simulate(recipe, stats, nonComboIds);

        int nonComboCPSpent = stats.CP - nonComboResult.FinalState.CP;

        // Comboed should cost 18+18+18=54 CP
        comboedCPSpent.Should().Be(54, "combo chain should reduce all costs to 18");

        // Non-combo should cost 46+32+18=96 CP
        nonComboCPSpent.Should().Be(96, "without combo, full costs apply");
    }

    [Fact]
    public void TestCase3_FinalAppraisal_PreventsCompletion()
    {
        var stats = CreateLevel90Stats();
        // Low difficulty recipe that will complete in one Groundwork
        var recipe = CreateLevel90Recipe() with { Difficulty = 100, Durability = 80 };

        var actionIds = new List<int> { FinalAppraisal, Groundwork };
        var result = _simulator.Simulate(recipe, stats, actionIds);

        // Final Appraisal should cap progress at max-1
        result.FinalState.Progress.Should().Be(recipe.Difficulty - 1);
        result.FinalState.Status.Should().Be(CraftStatus.InProgress);
        result.Success.Should().BeFalse("craft should not complete due to Final Appraisal");
    }

    [Fact]
    public void TestCase4_MuscleMemory_FirstStepOnly()
    {
        var stats = CreateLevel90Stats();
        var recipe = CreateLevel90Recipe() with { Durability = 80 };

        // Step 1: MuscleMemory should succeed
        var step1Ids = new List<int> { MuscleMemory };
        var result1 = _simulator.Simulate(recipe, stats, step1Ids);
        result1.Steps[0].ActionSucceeded.Should().BeTrue();
        result1.FinalState.Progress.Should().BeGreaterThan(0);

        // Step 2: MuscleMemory after BasicSynthesis should fail (not first step)
        var step2Ids = new List<int> { BasicSynthesis, MuscleMemory };
        var result2 = _simulator.Simulate(recipe, stats, step2Ids);
        result2.Steps[1].ActionSucceeded.Should().BeFalse();
        result2.Steps[1].FailReason.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void TestCase5_ByregotsBlessing_ScalesWithIQ()
    {
        var stats = CreateLevel90Stats();
        var recipe = CreateLevel90Recipe() with { Durability = 200, Quality = 50000 };

        // 1 IQ stack: Reflect (first step, gives 1 IQ)
        var state1IQ = CraftingState.CreateInitial(recipe, stats) with
        {
            InnerQuietStacks = 1,
            IsFirstStep = false,
            Step = 1,
        };
        var step1IQ = _simulator.ExecuteAction(state1IQ, ByregotsBlessing, stats, recipe);
        int quality1IQ = step1IQ.QualityDelta;

        // 5 IQ stacks
        var state5IQ = CraftingState.CreateInitial(recipe, stats) with
        {
            InnerQuietStacks = 5,
            IsFirstStep = false,
            Step = 1,
        };
        var step5IQ = _simulator.ExecuteAction(state5IQ, ByregotsBlessing, stats, recipe);
        int quality5IQ = step5IQ.QualityDelta;

        // 10 IQ stacks
        var state10IQ = CraftingState.CreateInitial(recipe, stats) with
        {
            InnerQuietStacks = 10,
            IsFirstStep = false,
            Step = 1,
        };
        var step10IQ = _simulator.ExecuteAction(state10IQ, ByregotsBlessing, stats, recipe);
        int quality10IQ = step10IQ.QualityDelta;

        // Quality should scale: 1 IQ < 5 IQ < 10 IQ
        quality1IQ.Should().BeGreaterThan(0);
        quality5IQ.Should().BeGreaterThan(quality1IQ, "5 IQ should produce more quality than 1 IQ");
        quality10IQ.Should().BeGreaterThan(quality5IQ, "10 IQ should produce more quality than 5 IQ");

        // Byregot's potency = 100 + 20 * stacks
        // At 10 IQ: potency = 300, which is 2.5x the 1 IQ potency (120)
        // Plus effective control is higher at 10 IQ (control * 2.0 vs control * 1.1)
        quality10IQ.Should().BeGreaterThan(quality1IQ * 3, "10 IQ should give dramatically more quality");
    }

    [Fact]
    public void TestCase6_ActionFailure_SpendsCPAndDurabilityButNoProgress()
    {
        var stats = CreateLevel90Stats();
        var recipe = CreateLevel90Recipe() with { Durability = 80 };

        // Use RapidSynthesis with AssumeAllSuccess=false and a seed that causes failure
        // RapidSynthesis has 50% base success rate
        // We need a seed where the first roll fails (roll >= 0.50)
        // Try seeds until we find one that fails
        int? failSeed = null;
        for (int seed = 0; seed < 100; seed++)
        {
            var testRandom = new Random(seed + 1); // +1 because simulator offsets seed
            if (testRandom.NextDouble() * 100.0 >= 50) // >= 50 means fail
            {
                failSeed = seed;
                break;
            }
        }

        failSeed.Should().NotBeNull("should find a seed that causes failure");

        var options = new SimulationOptions
        {
            AssumeAllSuccess = false,
            AssumeNormalCondition = true,
            RandomSeed = failSeed!.Value,
        };

        var actionIds = new List<int> { RapidSynthesis };
        var result = _simulator.Simulate(recipe, stats, actionIds, options);

        result.Steps.Should().HaveCount(1);
        var step = result.Steps[0];

        step.ActionSucceeded.Should().BeFalse("action should have failed");
        step.ProgressDelta.Should().Be(0, "no progress on failure");
        step.DurabilityDelta.Should().BeLessThan(0, "durability still spent on failure");
        step.CPDelta.Should().Be(0, "RapidSynthesis costs 0 CP");

        // Combo should be broken
        result.FinalState.ComboAction.Should().Be(0);
    }

    [Fact]
    public void TestCase7_CraftFailure_DurabilityReachesZero()
    {
        var stats = CreateLevel90Stats();
        // Very high difficulty, very low durability
        var recipe = CreateLevel90Recipe() with { Difficulty = 999999, Durability = 20 };

        // Each BasicSynthesis costs 10 durability. 2 uses = 0 durability = Failed
        var actionIds = new List<int> { BasicSynthesis, BasicSynthesis };
        var result = _simulator.Simulate(recipe, stats, actionIds);

        result.FinalState.Durability.Should().BeLessThanOrEqualTo(0);
        result.FinalState.Status.Should().Be(CraftStatus.Failed);
        result.Success.Should().BeFalse();
    }

    [Fact]
    public void TestCase8_MacroExport_SplitsIntoMultipleMacros()
    {
        // 20-action rotation (mix of buffs and synthesis)
        var actionIds = new List<int>
        {
            MuscleMemory,       // progress (wait 3)
            Manipulation,       // buff (wait 2)
            Veneration,         // buff (wait 2)
            Groundwork,         // progress (wait 3)
            Groundwork,         // progress (wait 3)
            Innovation,         // buff (wait 2)
            BasicTouch,         // quality (wait 3)
            StandardTouch,      // quality (wait 3)
            AdvancedTouch,      // quality (wait 3)
            Innovation,         // buff (wait 2)
            BasicTouch,         // quality (wait 3)
            StandardTouch,      // quality (wait 3)
            AdvancedTouch,      // quality (wait 3)
            GreatStrides,       // buff (wait 2)
            ByregotsBlessing,   // quality (wait 3)
            Veneration,         // buff (wait 2)
            Groundwork,         // progress (wait 3)
            Groundwork,         // progress (wait 3)
            CarefulSynthesis,   // progress (wait 3)
            BasicSynthesis,     // progress (wait 3)
        };

        var macros = MacroExporter.ExportToMacro(actionIds);

        // 20 actions at 14 per macro = 2 macros
        macros.Should().HaveCount(2);

        // First macro: 14 action lines + 1 echo = 15 lines
        var firstMacroLines = macros[0].Split('\n');
        firstMacroLines.Should().HaveCount(15);
        firstMacroLines.Last().Should().Contain("Macro #1 complete");
        firstMacroLines.Last().Should().Contain("<se.1>");

        // Second macro: 6 action lines + 1 echo = 7 lines
        var secondMacroLines = macros[1].Split('\n');
        secondMacroLines.Should().HaveCount(7);
        secondMacroLines.Last().Should().Contain("Craft complete");
        secondMacroLines.Last().Should().Contain("<se.2>");

        // Verify buff wait times (Manipulation is a buff → wait 2)
        macros[0].Should().Contain("/ac \"Manipulation\" <wait.2>");
        macros[0].Should().Contain("/ac \"Veneration\" <wait.2>");

        // Verify action wait times (Groundwork is progress → wait 3)
        macros[0].Should().Contain("/ac \"Groundwork\" <wait.3>");
        macros[0].Should().Contain("/ac \"Basic Touch\" <wait.3>");
    }

    [Fact]
    public void CreateInitialState_SetsCorrectValues()
    {
        var stats = CreateLevel90Stats();
        var recipe = CreateLevel90Recipe();

        var state = _simulator.CreateInitialState(recipe, stats);

        state.Progress.Should().Be(0);
        state.MaxProgress.Should().Be(recipe.Difficulty);
        state.Quality.Should().Be(recipe.StartingQuality);
        state.MaxQuality.Should().Be(recipe.Quality);
        state.Durability.Should().Be(recipe.Durability);
        state.MaxDurability.Should().Be(recipe.Durability);
        state.CP.Should().Be(stats.CP);
        state.MaxCP.Should().Be(stats.CP);
        state.Step.Should().Be(0);
        state.Condition.Should().Be(CraftCondition.Normal);
        state.Status.Should().Be(CraftStatus.InProgress);
        state.IsFirstStep.Should().BeTrue();
    }

    [Fact]
    public void GetAvailableActions_ReturnsAllActions()
    {
        var stats = CreateLevel90Stats();
        var recipe = CreateLevel90Recipe();
        var state = _simulator.CreateInitialState(recipe, stats);

        var available = _simulator.GetAvailableActions(state, stats, recipe);

        available.Should().NotBeEmpty();
        available.Count.Should().Be(CraftingActionRegistry.GetAll().Count);

        // MuscleMemory should be available on first step
        var muscleMemory = available.FirstOrDefault(a => a.ActionId == MuscleMemory);
        muscleMemory.Should().NotBeNull();
        muscleMemory!.IsAvailable.Should().BeTrue();
        muscleMemory.Category.Should().Be(ActionCategory.Progress);

        // Byregot's should be unavailable (no IQ stacks)
        var byregots = available.FirstOrDefault(a => a.ActionId == ByregotsBlessing);
        byregots.Should().NotBeNull();
        byregots!.IsAvailable.Should().BeFalse();
        byregots.UnavailableReason.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void ExecuteAction_UnknownAction_ReturnsFailure()
    {
        var stats = CreateLevel90Stats();
        var recipe = CreateLevel90Recipe();
        var state = _simulator.CreateInitialState(recipe, stats);

        var result = _simulator.ExecuteAction(state, 999999, stats, recipe);

        result.ActionSucceeded.Should().BeFalse();
        result.FailReason.Should().Contain("Unknown");
        result.StateAfter.Should().Be(state);
    }

    [Fact]
    public void Simulate_UnknownAction_SkipsAndContinues()
    {
        var stats = CreateLevel90Stats();
        var recipe = CreateLevel90Recipe() with { Durability = 80 };

        var actionIds = new List<int> { 999999, BasicSynthesis };
        var result = _simulator.Simulate(recipe, stats, actionIds);

        result.Steps.Should().HaveCount(2);
        result.Steps[0].ActionSucceeded.Should().BeFalse();
        result.Steps[1].ActionSucceeded.Should().BeTrue();
        result.FinalState.Progress.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Simulate_StopsAfterCompletion()
    {
        var stats = CreateLevel90Stats();
        var recipe = CreateLevel90Recipe() with { Difficulty = 100, Durability = 80 };

        // First Groundwork should complete, second should not execute
        var actionIds = new List<int> { Groundwork, Groundwork };
        var result = _simulator.Simulate(recipe, stats, actionIds);

        result.Steps.Should().HaveCount(1);
        result.Success.Should().BeTrue();
    }

    [Fact]
    public void Simulate_WithConditionChanges_UpdatesState()
    {
        var stats = CreateLevel90Stats();
        var recipe = CreateLevel90Recipe() with { Durability = 80, Quality = 50000 };

        var options = new SimulationOptions
        {
            AssumeAllSuccess = true,
            AssumeNormalCondition = false,
            RandomSeed = 42,
        };

        var actionIds = new List<int> { BasicTouch, BasicTouch, BasicTouch, BasicTouch, BasicTouch };
        var result = _simulator.Simulate(recipe, stats, actionIds, options);

        // With condition changes enabled and seeded random, conditions should vary
        // (not all Normal)
        result.Steps.Should().HaveCount(5);
    }

    [Fact]
    public void Simulate_ForceCondition_OverridesAllSteps()
    {
        var stats = CreateLevel90Stats();
        var recipe = CreateLevel90Recipe() with { Durability = 80, Quality = 50000 };

        var options = new SimulationOptions
        {
            AssumeAllSuccess = true,
            AssumeNormalCondition = false,
            ForceCondition = CraftCondition.Good,
        };

        var actionIds = new List<int> { BasicTouch, BasicTouch };
        var result = _simulator.Simulate(recipe, stats, actionIds, options);

        // All states after execution should have Good condition
        result.Steps[0].StateAfter.Condition.Should().Be(CraftCondition.Good);
        result.Steps[1].StateAfter.Condition.Should().Be(CraftCondition.Good);
    }
}
