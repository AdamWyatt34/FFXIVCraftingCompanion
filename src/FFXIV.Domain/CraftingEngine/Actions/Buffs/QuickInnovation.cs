using FFXIV.Domain.CraftingEngine.Models;

namespace FFXIV.Domain.CraftingEngine.Actions.Buffs;

public class QuickInnovation : BuffAction
{
    public override int Id => 100443;
    public override string Name => "QuickInnovation";
    public override string InGameName => "Quick Innovation";
    public override int LevelRequirement => 96;
    protected override int BaseCPCost => 0;
    protected override int BuffDuration => 0;

    public override string? GetFailReason(
        CraftingState state, CrafterStats stats, RecipeParameters recipe)
    {
        if (state.QuickInnovationUsed)
            return "Quick Innovation can only be used once per craft";
        if (state.InnovationTurns > 0)
            return "Cannot be used while Innovation is active";
        return base.GetFailReason(state, stats, recipe);
    }

    protected override CraftingState ApplyEffect(
        CraftingState state, CrafterStats stats, RecipeParameters recipe)
    {
        return state with
        {
            InnovationTurns = 1,
            QuickInnovationUsed = true,
        };
    }
}
