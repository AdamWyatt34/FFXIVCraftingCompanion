using FFXIV.Domain.CraftingEngine.Models;

namespace FFXIV.Domain.CraftingEngine.Actions.Quality;

public class TrainedEye : QualityAction
{
    public override int Id => 100283;
    public override string Name => "TrainedEye";
    public override string InGameName => "Trained Eye";
    public override int LevelRequirement => 80;
    protected override int BaseCPCost => 250;
    protected override int BaseDurabilityCost => 0;
    protected override int Potency => 0;

    public override string? GetFailReason(
        CraftingState state, CrafterStats stats, RecipeParameters recipe)
    {
        if (!state.IsFirstStep)
            return "Can only be used as the first action";
        if (stats.Level < recipe.ClassJobLevel + 10)
            return $"Requires crafter level {recipe.ClassJobLevel + 10} or higher";
        return base.GetFailReason(state, stats, recipe);
    }

    protected override CraftingState ApplyEffect(
        CraftingState state, CrafterStats stats, RecipeParameters recipe)
    {
        return state with
        {
            Quality = state.MaxQuality,
        };
    }
}
