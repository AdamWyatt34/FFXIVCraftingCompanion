using FFXIV.Domain.CraftingEngine.Models;

namespace FFXIV.Domain.CraftingEngine.Actions.Quality;

// Raphael raphael-sim/src/actions.rs:392-438; Teamcraft quality/precise-touch.ts.
public class PreciseTouch : QualityAction
{
    // Our id, not the game's 100128; see the action-id convention in CLAUDE.md.
    public const int ActionId = 100501;

    public override int Id => ActionId;
    public override string Name => "PreciseTouch";
    public override string InGameName => "Precise Touch";
    public override int LevelRequirement => 53;
    protected override int BaseCPCost => 18;
    protected override int Potency => 150;

    public override string? GetFailReason(
        CraftingState state, CrafterStats stats, RecipeParameters recipe)
    {
        if (state.Condition != CraftCondition.Good &&
            state.Condition != CraftCondition.Excellent &&
            !state.HeartAndSoulActive)
            return "Requires Good or Excellent condition";
        return base.GetFailReason(state, stats, recipe);
    }

    protected override int GetInnerQuietGain() => 2;

    protected override CraftingState ApplyEffect(
        CraftingState state, CrafterStats stats, RecipeParameters recipe)
    {
        var result = base.ApplyEffect(state, stats, recipe);
        if (state.HeartAndSoulActive &&
            state.Condition != CraftCondition.Good &&
            state.Condition != CraftCondition.Excellent)
            result = result with { HeartAndSoulActive = false };
        return result;
    }
}
