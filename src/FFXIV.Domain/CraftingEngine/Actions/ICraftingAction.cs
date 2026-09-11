using FFXIV.Domain.CraftingEngine.Models;

namespace FFXIV.Domain.CraftingEngine.Actions;

public interface ICraftingAction
{
    int Id { get; }
    string Name { get; }
    string InGameName { get; }
    int LevelRequirement { get; }
    ActionCategory Category { get; }

    /// <summary>
    /// False for the abilities the game resolves without advancing the craft (Heart and Soul,
    /// Quick Innovation, Careful Observation, Final Appraisal): no buff tick, no step count,
    /// first-step actions stay available afterwards.
    /// </summary>
    bool ConsumesStep { get; }

    /// <summary>
    /// Whether the condition rolls again after this action. Only Careful Observation rerolls
    /// without consuming a step; that is its entire purpose.
    /// </summary>
    bool AdvancesCondition { get; }

    int GetCPCost(CraftingState state);
    int GetDurabilityCost(CraftingState state);
    int GetSuccessRate(CraftingState state);

    bool CanExecute(CraftingState state, CrafterStats stats, RecipeParameters recipe);
    CraftingState Execute(CraftingState state, CrafterStats stats, RecipeParameters recipe, bool forceSuccess = false);
    string? GetFailReason(CraftingState state, CrafterStats stats, RecipeParameters recipe);
}
