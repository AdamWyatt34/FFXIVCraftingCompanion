using FFXIV.Domain.CraftingEngine.Models;

namespace FFXIV.Domain.CraftingEngine.Actions.Quality;

// Raphael raphael-sim/src/actions.rs:1142-1177; Teamcraft quality/daring-touch.ts.
public class DaringTouch : QualityAction
{
    // Our id, not the game's 100451, which is Trained Perfection here.
    public const int ActionId = 100503;
    public const int RequiredLevel = 96;

    public override int Id => ActionId;
    public override string Name => "DaringTouch";
    public override string InGameName => "Daring Touch";
    public override int LevelRequirement => RequiredLevel;

    // Under Expedience the Hasty Touch slot becomes Daring Touch, so a macro calls it by Hasty Touch's name
    // (Raphael raphael-data/src/locales.rs macro_name; Teamcraft commit a21776a).
    public override string MacroName => "Hasty Touch";
    protected override int BaseCPCost => 0;
    protected override int BaseSuccessRate => 60;
    protected override int Potency => 150;

    public override string? GetFailReason(
        CraftingState state, CrafterStats stats, RecipeParameters recipe)
    {
        if (!state.ExpedienceActive)
            return "Requires Expedience (a successful Hasty Touch)";
        return base.GetFailReason(state, stats, recipe);
    }
}
