using FFXIV.Domain.CraftingEngine.Models;

namespace FFXIV.Domain.CraftingEngine.Actions;

public interface ICraftingAction
{
    int Id { get; }
    string Name { get; }
    string InGameName { get; }
    int LevelRequirement { get; }
    ActionCategory Category { get; }

    // False for the abilities the game resolves without advancing the craft (Heart and Soul,
    // Quick Innovation, Careful Observation, Final Appraisal): no buff tick, no step count,
    // first-step actions stay available afterwards.
    bool ConsumesStep { get; }

    // Only Careful Observation rerolls the condition without consuming a step; that is its
    // entire purpose.
    bool AdvancesCondition { get; }

    // The macro's <wait.N>: 2 seconds for buffs, 3 for every other action including Observe, Heart and Soul,
    // Quick Innovation and Trained Perfection (Raphael raphael-sim/src/actions.rs time_cost; Teamcraft getWaitDuration).
    int MacroWaitSeconds { get; }

    // The name a macro's /ac line uses; it differs from InGameName only for an action that takes over another's
    // hotbar slot, which the game resolves by the original name.
    string MacroName { get; }

    // Removed from the game but kept resolvable by id so saved rotations and macros still load;
    // never offered by the registry's GetAll/GetAvailable or picked by a solver.
    bool IsLegacy { get; }

    int GetCPCost(CraftingState state);
    int GetDurabilityCost(CraftingState state);
    int GetSuccessRate(CraftingState state);

    bool CanExecute(CraftingState state, CrafterStats stats, RecipeParameters recipe);
    CraftingState Execute(CraftingState state, CrafterStats stats, RecipeParameters recipe, bool forceSuccess = false);
    string? GetFailReason(CraftingState state, CrafterStats stats, RecipeParameters recipe);
}
