using FFXIV.Domain.CraftingEngine.Models;

namespace FFXIV.Domain.CraftingEngine.Actions.Quality;

public class PrudentTouch : QualityAction
{
    public override int Id => 100227;
    public override string Name => "PrudentTouch";
    public override string InGameName => "Prudent Touch";
    public override int LevelRequirement => 66;
    protected override int BaseCPCost => 25;
    protected override int BaseDurabilityCost => 5;
    protected override int Potency => 100;

    public override string? GetFailReason(
        CraftingState state, CrafterStats stats, RecipeParameters recipe)
    {
        if (state.WasteNotTurns > 0 || state.WasteNotIITurns > 0)
            return "Cannot be used while Waste Not is active";
        return base.GetFailReason(state, stats, recipe);
    }
}
