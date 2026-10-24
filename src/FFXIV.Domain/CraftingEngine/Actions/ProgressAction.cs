using FFXIV.Domain.CraftingEngine.Models;

namespace FFXIV.Domain.CraftingEngine.Actions;

public abstract class ProgressAction : CraftingActionBase
{
    public override ActionCategory Category => ActionCategory.Progress;
    protected override int BaseDurabilityCost => 10;
    protected override int BaseSuccessRate => 100;

    // Mastery traits raise the potency at the crafter's level for some actions.
    protected abstract int GetPotency(CraftingState state, CrafterStats stats);

    protected override CraftingState ApplyEffect(
        CraftingState state, CrafterStats stats, RecipeParameters recipe)
    {
        var progressIncrease = CalculateProgress(state, stats, recipe, GetPotency(state, stats));
        var newProgress = state.Progress + progressIncrease;

        // Final Appraisal: cap progress at MaxProgress - 1
        if (state.FinalAppraisalTurns > 0 && newProgress >= state.MaxProgress)
        {
            return state with
            {
                Progress = state.MaxProgress - 1,
                FinalAppraisalTurns = 0,
                MuscleMemoryTurns = 0,
            };
        }

        return state with
        {
            Progress = Math.Min(newProgress, state.MaxProgress),
            MuscleMemoryTurns = 0,
        };
    }

    // Buffs sum into one multiplier on the potency, in Raphael's integer order (raphael-sim/src/effects.rs:70-80,
    // actions.rs:28-52; Teamcraft progress-action.ts:26-36); Delicate Synthesis shares it.
    protected internal static int CalculateProgress(
        CraftingState state, CrafterStats stats, RecipeParameters recipe, int potency)
    {
        var baseProgress = FormulaCalculator.CalculateBaseProgress(
            stats.Craftsmanship, recipe.ProgressDivider, recipe.ProgressModifier,
            stats.Level, recipe.ClassJobLevel);

        var modifier = 10;
        if (state.MuscleMemoryTurns > 0)
            modifier += 10;
        if (state.VenerationTurns > 0)
            modifier += 5;

        return (int)((long)baseProgress * potency * modifier / 1000);
    }
}
