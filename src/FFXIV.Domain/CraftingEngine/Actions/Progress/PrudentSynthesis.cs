using FFXIV.Domain.CraftingEngine.Models;

namespace FFXIV.Domain.CraftingEngine.Actions.Progress;

public class PrudentSynthesis : ProgressAction
{
    public override int Id => 100427;
    public override string Name => "PrudentSynthesis";
    public override string InGameName => "Prudent Synthesis";
    public override int LevelRequirement => 88;
    protected override int BaseCPCost => 18;
    protected override int BaseDurabilityCost => 5;
    protected override int Potency => 180;

    public override string? GetFailReason(
        CraftingState state, CrafterStats stats, RecipeParameters recipe)
    {
        if (state.WasteNotTurns > 0 || state.WasteNotIITurns > 0)
            return "Cannot be used while Waste Not is active";
        return base.GetFailReason(state, stats, recipe);
    }
}
