using FFXIV.Domain.CraftingEngine.Models;

namespace FFXIV.Domain.CraftingEngine.Actions.Progress;

public class Groundwork : ProgressAction
{
    public override int Id => 100403;
    public override string Name => "Groundwork";
    public override string InGameName => "Groundwork";
    public override int LevelRequirement => 72;
    protected override int BaseCPCost => 18;
    protected override int BaseDurabilityCost => 20;
    // Groundwork Mastery, unlocked at level 86.
    protected override int GetPotency(CraftingState state, CrafterStats stats) => stats.Level >= 86 ? 360 : 300;

    protected override CraftingState ApplyEffect(
        CraftingState state, CrafterStats stats, RecipeParameters recipe)
    {
        var basePotency = GetPotency(state, stats);
        var effectivePotency = state.Durability < GetDurabilityCost(state) ? basePotency / 2 : basePotency;

        var newProgress = state.Progress + CalculateProgress(state, stats, recipe, effectivePotency);

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
}
