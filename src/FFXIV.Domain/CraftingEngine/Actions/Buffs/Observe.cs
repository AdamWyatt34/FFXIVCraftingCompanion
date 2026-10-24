using FFXIV.Domain.CraftingEngine.Actions.Quality;
using FFXIV.Domain.CraftingEngine.Models;

namespace FFXIV.Domain.CraftingEngine.Actions.Buffs;

public class Observe : BuffAction
{
    public override int Id => 100010;
    public override string Name => "Observe";
    public override string InGameName => "Observe";
    public override int LevelRequirement => 13;
    public override int MacroWaitSeconds => 3;
    protected override int BaseCPCost => 7;
    protected override int BuffDuration => 0;

    // Observe is a combo action since 7.0 and opens Advanced Touch's discount
    // (Raphael raphael-sim/src/actions.rs:158-172; Teamcraft quality/advanced-touch.ts hasCombo).
    protected override int GetComboAction(CraftingState before) => StandardTouch.ActionId;

    protected override CraftingState ApplyEffect(
        CraftingState state, CrafterStats stats, RecipeParameters recipe)
    {
        return state;
    }
}
