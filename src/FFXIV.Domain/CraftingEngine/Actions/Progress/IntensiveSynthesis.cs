using FFXIV.Domain.CraftingEngine.Models;

namespace FFXIV.Domain.CraftingEngine.Actions.Progress;

public class IntensiveSynthesis : ProgressAction
{
    public override int Id => 100315;
    public override string Name => "IntensiveSynthesis";
    public override string InGameName => "Intensive Synthesis";
    public override int LevelRequirement => 78;
    protected override int BaseCPCost => 6;
    protected override int GetPotency(CraftingState state, CrafterStats stats) => 400;

    public override string? GetFailReason(
        CraftingState state, CrafterStats stats, RecipeParameters recipe)
    {
        if (state.Condition != CraftCondition.Good &&
            state.Condition != CraftCondition.Excellent &&
            !state.HeartAndSoulActive)
            return "Requires Good or Excellent condition";
        return base.GetFailReason(state, stats, recipe);
    }

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
