using FFXIV.Domain.CraftingEngine.Models;

namespace FFXIV.Domain.CraftingEngine.Actions.Progress;

public class DelicateSynthesis : CraftingActionBase
{
    public override int Id => 100323;
    public override string Name => "DelicateSynthesis";
    public override string InGameName => "Delicate Synthesis";
    public override int LevelRequirement => 76;
    public override ActionCategory Category => ActionCategory.Progress;
    protected override int BaseCPCost => 32;
    protected override int BaseDurabilityCost => 10;
    protected override int BaseSuccessRate => 100;

    protected override CraftingState ApplyEffect(
        CraftingState state, CrafterStats stats, RecipeParameters recipe)
    {
        // Delicate Synthesis Mastery (level 94) raises only the progress half, not quality.
        var baseProgress = FormulaCalculator.CalculateBaseProgress(
            stats.Craftsmanship, recipe.ProgressDivider, recipe.ProgressModifier,
            stats.Level, recipe.RecipeLevel);

        var progressEfficiency = stats.Level >= 94 ? 150 : 100;
        if (state.VenerationTurns > 0)
            progressEfficiency += 50;
        if (state.MuscleMemoryTurns > 0)
            progressEfficiency += 100;

        var progressIncrease = (int)(baseProgress * progressEfficiency / 100.0);

        // Calculate quality (100 potency)
        var effectiveControl = stats.GetEffectiveControl(state.InnerQuietStacks);
        var baseQuality = FormulaCalculator.CalculateBaseQuality(
            effectiveControl, recipe.QualityDivider, recipe.QualityModifier,
            stats.Level, recipe.RecipeLevel);

        var qualityEfficiency = 100;
        if (state.InnovationTurns > 0)
            qualityEfficiency += 50;
        if (state.GreatStridesTurns > 0)
            qualityEfficiency += 100;

        var qualityIncrease = (int)(baseQuality * qualityEfficiency / 100.0);

        qualityIncrease = state.Condition switch
        {
            CraftCondition.Good => (int)(qualityIncrease * 1.5),
            CraftCondition.Excellent => qualityIncrease * 4,
            CraftCondition.Poor => (int)(qualityIncrease * 0.5),
            _ => qualityIncrease,
        };

        // Handle Final Appraisal for progress
        var newProgress = state.Progress + progressIncrease;
        var finalAppraisalTurns = state.FinalAppraisalTurns;
        if (finalAppraisalTurns > 0 && newProgress >= state.MaxProgress)
        {
            newProgress = state.MaxProgress - 1;
            finalAppraisalTurns = 0;
        }
        else
        {
            newProgress = Math.Min(newProgress, state.MaxProgress);
        }

        return state with
        {
            Progress = newProgress,
            Quality = Math.Min(state.Quality + qualityIncrease, state.MaxQuality),
            FinalAppraisalTurns = finalAppraisalTurns,
            MuscleMemoryTurns = 0,
            GreatStridesTurns = 0,
            InnerQuietStacks = Math.Min(state.InnerQuietStacks + 1, 10),
        };
    }
}
