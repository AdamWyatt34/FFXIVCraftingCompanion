using FFXIV.Domain.CraftingEngine.Models;

namespace FFXIV.Domain.CraftingEngine.Actions.Buffs;

public class TrainedPerfection : BuffAction
{
    public override int Id => 100451;
    public override string Name => "TrainedPerfection";
    public override string InGameName => "Trained Perfection";
    public override int LevelRequirement => 100;
    public override int MacroWaitSeconds => 3;
    protected override int BaseCPCost => 0;
    protected override int BuffDuration => 0;

    public override string? GetFailReason(
        CraftingState state, CrafterStats stats, RecipeParameters recipe)
    {
        if (state.TrainedPerfectionUsed)
            return "Trained Perfection can only be used once per craft";
        return base.GetFailReason(state, stats, recipe);
    }

    protected override CraftingState ApplyEffect(
        CraftingState state, CrafterStats stats, RecipeParameters recipe)
    {
        return state with
        {
            TrainedPerfectionActive = true,
            TrainedPerfectionUsed = true,
        };
    }
}
