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

    int GetCPCost(CraftingState state);
    int GetDurabilityCost(CraftingState state);
    int GetSuccessRate(CraftingState state);

    bool CanExecute(CraftingState state, CrafterStats stats, RecipeParameters recipe);
    CraftingState Execute(CraftingState state, CrafterStats stats, RecipeParameters recipe, bool forceSuccess = false);
    string? GetFailReason(CraftingState state, CrafterStats stats, RecipeParameters recipe);
}
