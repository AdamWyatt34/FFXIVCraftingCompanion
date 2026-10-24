using FFXIV.Domain.CraftingEngine.Models;

namespace FFXIV.Domain.CraftingEngine.Actions.Quality;

// Usable outside the combo, like every combo action in game; only the extra Inner Quiet needs Basic Touch
// (Teamcraft quality/refined-touch.ts; Raphael raphael-sim/src/actions.rs:923-966 prunes the uncomboed use).
public class RefinedTouch : QualityAction
{
    // Our id, not the game's 100443, which is Quick Innovation here.
    public const int ActionId = 100502;

    public override int Id => ActionId;
    public override string Name => "RefinedTouch";
    public override string InGameName => "Refined Touch";
    public override int LevelRequirement => 92;
    protected override int BaseCPCost => 24;
    protected override int Potency => 100;

    protected override CraftingState ApplyEffect(
        CraftingState state, CrafterStats stats, RecipeParameters recipe)
    {
        var result = base.ApplyEffect(state, stats, recipe);
        if (state.ComboAction != BasicTouch.ActionId)
            return result;
        return result with { InnerQuietStacks = GainInnerQuiet(result.InnerQuietStacks, 1, stats) };
    }
}
