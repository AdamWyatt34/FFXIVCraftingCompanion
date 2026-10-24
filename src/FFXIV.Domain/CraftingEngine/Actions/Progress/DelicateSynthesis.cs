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
        var progressIncrease = ProgressAction.CalculateProgress(state, stats, recipe, stats.Level >= 94 ? 150 : 100);
        var qualityIncrease = QualityAction.CalculateQuality(state, stats, recipe, 100);

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
            InnerQuietStacks = QualityAction.GainInnerQuiet(state.InnerQuietStacks, 1, stats),
        };
    }
}
