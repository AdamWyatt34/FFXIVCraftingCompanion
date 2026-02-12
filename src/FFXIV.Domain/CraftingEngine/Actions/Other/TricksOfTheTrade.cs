using FFXIV.Domain.CraftingEngine.Models;

namespace FFXIV.Domain.CraftingEngine.Actions.Other;

public class TricksOfTheTrade : CraftingActionBase
{
    public override int Id => 100371;
    public override string Name => "TricksOfTheTrade";
    public override string InGameName => "Tricks of the Trade";
    public override int LevelRequirement => 13;
    public override ActionCategory Category => ActionCategory.Other;
    protected override int BaseCPCost => 0;
    protected override int BaseDurabilityCost => 0;
    protected override int BaseSuccessRate => 100;

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
        var result = state with
        {
            CP = Math.Min(state.CP + 20, state.MaxCP),
        };

        if (state.HeartAndSoulActive &&
            state.Condition != CraftCondition.Good &&
            state.Condition != CraftCondition.Excellent)
            result = result with { HeartAndSoulActive = false };

        return result;
    }
}
