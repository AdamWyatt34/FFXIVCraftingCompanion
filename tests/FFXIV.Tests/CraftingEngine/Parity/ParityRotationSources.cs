using FFXIV.Domain.CraftingEngine;
using FFXIV.Domain.CraftingEngine.Models;
using FFXIV.Domain.CraftingEngine.Solver.Models;
using FFXIV.Infrastructure.Solver;
using FFXIV.Tests.CraftingEngine.Solver;

namespace FFXIV.Tests.CraftingEngine.Parity;

public sealed record ParityScenario(
    string Name,
    RecipeParameters Recipe,
    CrafterStats Stats,
    SimulationOptions Options,
    List<int> Actions,
    bool IncludeAvailability);

// Every sequence is derived from the C# engine itself (random picks are made among the actions
// the engine reports legal), so the goldens describe what the engine does, not what a rotation
// author intended.
internal static class ParityRotationSources
{
    private const int RandomCaseCount = 200;
    private const int ForcedCasesPerCondition = 5;
    private const int SeededCaseCount = 20;
    private const int MaxSequenceLength = 40;
    private const int UnknownActionId = 999999;
    private const int AllExpertConditions = 0x7F;
    private static readonly int[] PartialExpertConditions = [0x05, 0x22, 0x41];
    private const int ForcedSeededCasesPerCondition = 2;
    private const int PartialMaskCases = 5;

    private static readonly SimulationOptions Defaults = new();

    public static IEnumerable<ParityScenario> ForBenchmark(BenchmarkEntry benchmark)
    {
        var recipe = BenchmarkLoader.ToRecipeParameters(benchmark.Recipe);
        var stats = BenchmarkLoader.ToCrafterStats(benchmark.Stats);

        yield return new ParityScenario("community", recipe, stats, Defaults,
            BenchmarkLoader.ResolveActionNames(benchmark.CommunityRotation.Actions), IncludeAvailability: true);

        yield return new ParityScenario("greedy", recipe, stats, Defaults, GreedyRotation(recipe, stats), IncludeAvailability: true);

        for (var i = 0; i < RandomCaseCount; i++)
        {
            yield return new ParityScenario($"random-{i:000}", recipe, stats, Defaults,
                RandomLegalSequence(recipe, stats, 1000 + i, force: null), IncludeAvailability: false);
        }

        foreach (var condition in Enum.GetValues<CraftCondition>())
        {
            var options = new SimulationOptions { ForceCondition = condition };
            for (var i = 0; i < ForcedCasesPerCondition; i++)
            {
                yield return new ParityScenario($"forced-{condition}-{i}", recipe, stats, options,
                    RandomLegalSequence(recipe, stats, 2000 + (int)condition * 10 + i, force: condition),
                    IncludeAvailability: i == 0);
            }

            // A gated action under a forced condition leaves the state (and condition) untouched,
            // and Good-gated actions become legal once the forced condition applies.
            yield return new ParityScenario($"forced-gated-{condition}", recipe, stats, options,
                new[] { "IntensiveSynthesis", "BasicSynthesis", "TricksOfTheTrade", "IntensiveSynthesis", "BasicTouch" }.Select(Id).ToList(),
                IncludeAvailability: true);

            for (var i = 0; i < ForcedSeededCasesPerCondition; i++)
            {
                var rolled = new SimulationOptions { ForceCondition = condition, AssumeAllSuccess = false, RandomSeed = 7000 + (int)condition * 10 + i };
                yield return new ParityScenario($"forced-seeded-{condition}-{i}", recipe, stats, rolled,
                    SeededLegalSequence(recipe, stats, rolled, 8000 + (int)condition * 10 + i), IncludeAvailability: false);
            }
        }

        for (var i = 0; i < SeededCaseCount; i++)
        {
            var options = new SimulationOptions { AssumeAllSuccess = false, AssumeNormalCondition = false, RandomSeed = 3000 + i };
            yield return new ParityScenario($"seeded-{i:00}", recipe, stats, options,
                SeededLegalSequence(recipe, stats, options, 4000 + i), IncludeAvailability: false);
        }

        // The success stream is seeded from RandomSeed + 1, which wraps here.
        var wrapping = new SimulationOptions { AssumeAllSuccess = false, AssumeNormalCondition = false, RandomSeed = int.MaxValue };
        yield return new ParityScenario("seeded-max", recipe, stats, wrapping,
            SeededLegalSequence(recipe, stats, wrapping, 4100), IncludeAvailability: false);

        if (benchmark.Recipe.ClassJobLevel >= 100)
        {
            var specialist = stats with { IsSpecialist = true };
            var expert = recipe with { IsExpert = true, ConditionsFlag = AllExpertConditions };
            for (var i = 0; i < SeededCaseCount; i++)
            {
                var options = new SimulationOptions { AssumeAllSuccess = false, AssumeNormalCondition = false, RandomSeed = 5000 + i };
                yield return new ParityScenario($"expert-seeded-{i:00}", expert, specialist, options,
                    SeededLegalSequence(expert, specialist, options, 6000 + i), IncludeAvailability: i == 0);
            }

            foreach (var mask in PartialExpertConditions)
            {
                var partial = recipe with { IsExpert = true, ConditionsFlag = mask };
                for (var i = 0; i < PartialMaskCases; i++)
                {
                    var options = new SimulationOptions { AssumeAllSuccess = false, AssumeNormalCondition = false, RandomSeed = 9000 + mask * 10 + i };
                    yield return new ParityScenario($"expert-mask{mask:x2}-seeded-{i}", partial, specialist, options,
                        SeededLegalSequence(partial, specialist, options, 9500 + mask * 10 + i), IncludeAvailability: false);
                }
            }
        }

        foreach (var edge in EdgeScenarios(recipe, stats))
            yield return edge;
    }

    public static ParityCase Build(ParityScenario scenario)
    {
        var simulator = new CraftingSimulator();
        var initial = simulator.CreateInitialState(scenario.Recipe, scenario.Stats);
        var result = simulator.Simulate(scenario.Recipe, scenario.Stats, scenario.Actions, scenario.Options);

        var finalAvailability = scenario.IncludeAvailability
            ? Availability(simulator, result.FinalState, scenario)
            : null;

        var steps = result.Steps.Select(s => new ParityStep(
            s.StepNumber,
            s.ActionId,
            s.ActionName,
            s.ActionSucceeded,
            s.FailReason,
            LegalIds(simulator, s.StateBefore, scenario),
            ToState(s.StateAfter),
            scenario.IncludeAvailability ? Availability(simulator, s.StateBefore, scenario) : null)).ToList();

        return new ParityCase(
            scenario.Name,
            ToRecipe(scenario.Recipe),
            ToStats(scenario.Stats),
            new ParityOptions(
                scenario.Options.AssumeAllSuccess,
                scenario.Options.AssumeNormalCondition,
                scenario.Options.RandomSeed,
                scenario.Options.ForceCondition.HasValue ? (int)scenario.Options.ForceCondition.Value : null),
            scenario.Actions,
            ToState(initial),
            steps,
            ToState(result.FinalState),
            finalAvailability,
            MacroExporter.ExportToMacro(scenario.Actions));
    }

    private static List<int> GreedyRotation(RecipeParameters recipe, CrafterStats stats)
    {
        var solver = new GreedySolver(new CraftingSimulator());
        var result = solver.SolveAsync(recipe, stats, new SolverConstraints(), new SolverSettings { Algorithm = SolverAlgorithm.Greedy })
            .GetAwaiter().GetResult();
        if (result.Status != SolverStatus.Completed)
            throw new InvalidOperationException($"Greedy solver did not complete: {result.ErrorMessage}");
        return result.ActionIds;
    }

    private static List<int> RandomLegalSequence(RecipeParameters recipe, CrafterStats stats, int seed, CraftCondition? force)
    {
        var simulator = new CraftingSimulator();
        var rng = new Random(seed);
        var state = simulator.CreateInitialState(recipe, stats);
        var ids = new List<int>();

        while (state.Status == CraftStatus.InProgress && ids.Count < MaxSequenceLength)
        {
            var legal = LegalIds(simulator, state, recipe, stats);
            if (legal.Count == 0) break;

            var pick = legal[rng.Next(legal.Count)];
            ids.Add(pick);
            state = simulator.ExecuteAction(state, pick, stats, recipe).StateAfter;
            if (force.HasValue)
                state = state with { Condition = force.Value };
        }

        return ids;
    }

    // Seeded runs roll conditions and success inside Simulate, so legality at step k is read
    // from Simulate(prefix) rather than from a hand-rolled loop; the RNG stream is a function
    // of the seed and the prefix length, so prefixes agree with the final full run.
    private static List<int> SeededLegalSequence(RecipeParameters recipe, CrafterStats stats, SimulationOptions options, int pickSeed)
    {
        var simulator = new CraftingSimulator();
        var rng = new Random(pickSeed);
        var ids = new List<int>();

        while (ids.Count < MaxSequenceLength)
        {
            var state = ids.Count == 0
                ? simulator.CreateInitialState(recipe, stats)
                : simulator.Simulate(recipe, stats, ids, options).FinalState;
            if (state.Status != CraftStatus.InProgress) break;

            var legal = LegalIds(simulator, state, recipe, stats);
            if (legal.Count == 0) break;
            ids.Add(legal[rng.Next(legal.Count)]);
        }

        return ids;
    }

    private static IEnumerable<ParityScenario> EdgeScenarios(RecipeParameters recipe, CrafterStats stats)
    {
        var lowLevel = stats with { Level = 10 };
        var starved = stats with { CP = 10 };
        var specialist = stats with { IsSpecialist = true };
        var underLevelForTrainedEye = stats with { Level = recipe.ClassJobLevel + 5 };
        var overLevelForTrainedEye = stats with { Level = recipe.ClassJobLevel + 10 };
        var expert = recipe with { IsExpert = true, ConditionsFlag = AllExpertConditions };
        var noJobLevel = recipe with { ClassJobLevel = 0 };

        ParityScenario Edge(string name, RecipeParameters r, CrafterStats s, params string[] actions) =>
            new($"edge-{name}", r, s, Defaults, actions.Select(Id).ToList(), IncludeAvailability: true);

        yield return Edge("first-step-only", recipe, stats, "BasicSynthesis", "MuscleMemory", "Reflect", "TrainedEye");
        yield return Edge("byregot-no-iq", recipe, stats, "ByregotsBlessing", "BasicTouch", "ByregotsBlessing");
        yield return Edge("finesse-under-ten-iq", recipe, stats, "BasicTouch", "TrainedFinesse");
        yield return Edge("good-only-on-normal", recipe, stats, "IntensiveSynthesis", "TricksOfTheTrade", "BasicSynthesis");
        yield return Edge("prudent-under-waste-not", recipe, stats, "WasteNot", "PrudentTouch", "PrudentSynthesis", "BasicTouch");
        yield return Edge("specialist-only", recipe, stats, "HeartAndSoul", "CarefulObservation", "QuickInnovation", "BasicSynthesis");
        yield return Edge("specialist-abilities-keep-first-step", recipe, specialist, "HeartAndSoul", "CarefulObservation", "MuscleMemory",
            "CarefulObservation", "CarefulObservation", "CarefulObservation", "Veneration", "Groundwork");
        yield return Edge("quick-innovation-limits", recipe, specialist, "QuickInnovation", "BasicTouch", "QuickInnovation", "Innovation", "QuickInnovation");
        yield return Edge("quick-innovation-blocked-by-innovation", recipe, specialist, "Innovation", "QuickInnovation", "BasicTouch", "BasicTouch", "BasicTouch", "BasicTouch", "QuickInnovation", "BasicTouch");
        yield return Edge("final-appraisal-is-step-free", recipe, stats, "Veneration", "FinalAppraisal", "FinalAppraisal", "BasicSynthesis", "MuscleMemory");
        yield return Edge("buffs-last-full-duration", recipe, stats, "Innovation", "BasicTouch", "BasicTouch", "BasicTouch", "BasicTouch", "BasicTouch",
            "Manipulation", "Manipulation", "Groundwork", "Groundwork");
        yield return Edge("trained-perfection-once", recipe, stats, "TrainedPerfection", "BasicTouch", "TrainedPerfection", "PreparatoryTouch");
        yield return Edge("level-gated", recipe, lowLevel, "BasicSynthesis", "CarefulSynthesis", "BasicTouch", "StandardTouch", "Veneration", "Groundwork");
        yield return Edge("cp-starved", recipe, starved, "Manipulation", "BasicSynthesis", "Veneration", "BasicTouch", "BasicSynthesis");
        yield return Edge("trained-eye-under-level", recipe, underLevelForTrainedEye, "TrainedEye", "BasicSynthesis");
        yield return Edge("trained-eye-over-level", recipe, overLevelForTrainedEye, "TrainedEye", "BasicSynthesis", "BasicSynthesis");
        yield return Edge("trained-eye-no-job-level", noJobLevel, overLevelForTrainedEye, "TrainedEye", "BasicSynthesis");
        yield return Edge("trained-eye-on-expert", expert, overLevelForTrainedEye with { IsSpecialist = true }, "TrainedEye", "HeartAndSoul", "HeartAndSoul",
            "CarefulObservation", "CarefulObservation", "CarefulObservation", "CarefulObservation", "IntensiveSynthesis", "TricksOfTheTrade");
        yield return Edge("manipulation-to-zero", recipe, stats, "Manipulation", "Groundwork", "Groundwork", "Groundwork", "Groundwork", "Groundwork", "Groundwork");
        yield return Edge("final-appraisal-cap", recipe, stats, "MuscleMemory", "FinalAppraisal", "Veneration", "Groundwork", "Groundwork", "Groundwork", "Groundwork", "Groundwork");
        yield return Edge("observe-focused-combo", recipe, stats, "Observe", "FocusedSynthesis", "Observe", "FocusedTouch", "FocusedTouch");
        yield return Edge("touch-combo", recipe, stats, "BasicTouch", "StandardTouch", "AdvancedTouch", "AdvancedTouch", "StandardTouch");
        yield return Edge("masters-mend-cap", recipe, stats, "MastersMend", "Groundwork", "MastersMend", "Groundwork", "Groundwork", "Groundwork", "ImmaculateMend");
        yield return Edge("delicate-with-buffs", recipe, stats, "Innovation", "Veneration", "GreatStrides", "DelicateSynthesis", "DelicateSynthesis");

        var unknownMidSequence = new List<int> { Id("BasicSynthesis"), UnknownActionId, Id("BasicTouch") };
        yield return new ParityScenario("edge-unknown-action", recipe, stats, Defaults, unknownMidSequence, IncludeAvailability: true);
    }

    private static int Id(string name) =>
        CraftingActionRegistry.GetByName(name)?.Id
        ?? throw new InvalidOperationException($"Unknown action name '{name}'");

    private static List<int> LegalIds(ICraftingSimulator simulator, CraftingState state, ParityScenario scenario) =>
        LegalIds(simulator, state, scenario.Recipe, scenario.Stats);

    private static List<int> LegalIds(ICraftingSimulator simulator, CraftingState state, RecipeParameters recipe, CrafterStats stats) =>
        simulator.GetAvailableActions(state, stats, recipe)
            .Where(a => a.IsAvailable)
            .Select(a => a.ActionId)
            .OrderBy(id => id)
            .ToList();

    private static List<ParityAvailability> Availability(ICraftingSimulator simulator, CraftingState state, ParityScenario scenario) =>
        simulator.GetAvailableActions(state, scenario.Stats, scenario.Recipe)
            .OrderBy(a => a.ActionId)
            .Select(a => new ParityAvailability(
                a.ActionId, a.IsAvailable, a.UnavailableReason, a.CPCost, a.DurabilityCost, a.SuccessRate, (int)a.Category))
            .ToList();

    private static ParityRecipe ToRecipe(RecipeParameters r) => new(
        r.RecipeId, r.RecipeLevel, r.ClassJobLevel, r.Difficulty, r.Quality, r.Durability, r.Stars,
        r.ProgressDivider, r.ProgressModifier, r.QualityDivider, r.QualityModifier,
        r.SuggestedCraftsmanship, r.SuggestedControl, r.IsExpert, r.ConditionsFlag,
        r.StartingQuality, r.MaterialQualityFactor);

    private static ParityStats ToStats(CrafterStats s) => new(s.Level, s.Craftsmanship, s.Control, s.CP, s.IsSpecialist);

    private static ParityState ToState(CraftingState s) => new(
        s.Progress, s.MaxProgress, s.Quality, s.MaxQuality, s.Durability, s.MaxDurability, s.CP, s.MaxCP,
        s.Step, (int)s.Condition, (int)s.Status,
        s.InnerQuietStacks, s.WasteNotTurns, s.WasteNotIITurns, s.VenerationTurns, s.InnovationTurns,
        s.GreatStridesTurns, s.ManipulationTurns, s.FinalAppraisalTurns, s.MuscleMemoryTurns,
        s.HeartAndSoulActive, s.TrainedPerfectionActive, s.QuickInnovationUsed, s.HeartAndSoulUsed,
        s.TrainedPerfectionUsed, s.CarefulObservationCount, s.IsFirstStep, s.ComboAction, s.ObserveActive,
        s.IsComplete, s.IsFailed, s.QualityPercent, s.ProgressPercent, s.HQPercent);
}
