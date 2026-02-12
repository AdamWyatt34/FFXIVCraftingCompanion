using FFXIV.Domain.CraftingEngine.Models;

namespace FFXIV.Domain.CraftingEngine.Actions;

public interface ICraftingAction
{
    int Id { get; }
    string Name { get; }
    string InGameName { get; }
    int LevelRequirement { get; }
    ActionCategory Category { get; }

    int GetCPCost(CraftingState state);
    int GetDurabilityCost(CraftingState state);
    int GetSuccessRate(CraftingState state);

    bool CanExecute(CraftingState state, CrafterStats stats, RecipeParameters recipe);
    CraftingState Execute(CraftingState state, CrafterStats stats, RecipeParameters recipe, bool forceSuccess = false);
    string? GetFailReason(CraftingState state, CrafterStats stats, RecipeParameters recipe);
}
