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
        var progressIncrease = CalculateProgress(state, stats, recipe);
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

    protected int CalculateProgress(CraftingState state, CrafterStats stats, RecipeParameters recipe)
    {
        var baseProgress = FormulaCalculator.CalculateBaseProgress(
            stats.Craftsmanship, recipe.ProgressDivider, recipe.ProgressModifier,
            stats.Level, recipe.RecipeLevel);

        var efficiency = GetPotency(state, stats);

        if (state.VenerationTurns > 0)
            efficiency += 50;

        if (state.MuscleMemoryTurns > 0)
            efficiency += 100;

        return (int)(baseProgress * efficiency / 100.0);
    }
}
