using FFXIV.Domain.CraftingEngine.Actions;
using FFXIV.Domain.CraftingEngine.Actions.Buffs;
using FFXIV.Domain.CraftingEngine.Actions.Other;
using FFXIV.Domain.CraftingEngine.Actions.Progress;
using FFXIV.Domain.CraftingEngine.Actions.Quality;
using FFXIV.Domain.CraftingEngine.Actions.Repair;
using FFXIV.Domain.CraftingEngine.Models;

namespace FFXIV.Domain.CraftingEngine;

public static class CraftingActionRegistry
{
    private static readonly Dictionary<int, ICraftingAction> ActionsById;
    private static readonly Dictionary<string, ICraftingAction> ActionsByName;

    static CraftingActionRegistry()
    {
        var allActions = new ICraftingAction[]
        {
            // Progress
            new BasicSynthesis(),
            new CarefulSynthesis(),
            new RapidSynthesis(),
            new FocusedSynthesis(),
            new Groundwork(),
            new IntensiveSynthesis(),
            new PrudentSynthesis(),
            new MuscleMemory(),
            new DelicateSynthesis(),

            // Quality
            new BasicTouch(),
            new StandardTouch(),
            new AdvancedTouch(),
            new HastyTouch(),
            new FocusedTouch(),
            new PreparatoryTouch(),
            new PrudentTouch(),
            new Reflect(),
            new ByregotsBlessing(),
            new TrainedFinesse(),
            new TrainedEye(),

            // Buffs
            new InnerQuiet(),
            new WasteNot(),
            new WasteNotII(),
            new Veneration(),
            new Innovation(),
            new GreatStrides(),
            new Manipulation(),
            new FinalAppraisal(),
            new Observe(),
            new HeartAndSoul(),
            new TrainedPerfection(),
            new QuickInnovation(),

            // Repair
            new MastersMend(),
            new ImmaculateMend(),

            // Other
            new TricksOfTheTrade(),
            new CarefulObservation(),
        };

        ActionsById = allActions.ToDictionary(a => a.Id);
        ActionsByName = new Dictionary<string, ICraftingAction>(StringComparer.OrdinalIgnoreCase);
        foreach (var action in allActions)
            ActionsByName[action.Name] = action;
    }

    public static ICraftingAction? GetById(int id) =>
        ActionsById.GetValueOrDefault(id);

    public static ICraftingAction? GetByName(string name) =>
        ActionsByName.GetValueOrDefault(name);

    public static IReadOnlyList<ICraftingAction> GetAll() =>
        ActionsById.Values.ToList();

    public static IReadOnlyList<ICraftingAction> GetAvailable(CrafterStats stats) =>
        ActionsById.Values
            .Where(a => stats.Level >= a.LevelRequirement)
            .OrderBy(a => a.Category)
            .ThenBy(a => a.LevelRequirement)
            .ToList();
}
