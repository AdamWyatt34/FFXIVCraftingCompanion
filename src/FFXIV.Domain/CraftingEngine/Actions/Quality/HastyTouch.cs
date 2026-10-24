using FFXIV.Domain.CraftingEngine.Models;

namespace FFXIV.Domain.CraftingEngine.Actions.Quality;

public class HastyTouch : QualityAction
{
    public override int Id => 100355;
    public override string Name => "HastyTouch";
    public override string InGameName => "Hasty Touch";
    public override int LevelRequirement => 9;
    protected override int BaseCPCost => 0;
    protected override int BaseSuccessRate => 60;
    protected override int Potency => 100;

    // Raphael raphael-sim/src/actions.rs HastyTouch transform; Teamcraft quality/hasty-touch.ts execute.
    protected override bool GrantsExpedience(CrafterStats stats) => stats.Level >= DaringTouch.RequiredLevel;

    // Under Expedience the game turns this slot into Daring Touch, so Hasty Touch itself cannot be used
    // (Raphael raphael-sim/src/actions.rs:1120-1123).
    public override string? GetFailReason(
        CraftingState state, CrafterStats stats, RecipeParameters recipe)
    {
        if (state.ExpedienceActive)
            return "Hasty Touch becomes Daring Touch under Expedience";
        return base.GetFailReason(state, stats, recipe);
    }
}
