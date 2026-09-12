using FFXIV.Domain.CraftingEngine.Models;

namespace FFXIV.Domain.CraftingEngine.Actions.Buffs;

public class HeartAndSoul : BuffAction
{
    public override int Id => 100317;
    public override string Name => "HeartAndSoul";
    public override string InGameName => "Heart and Soul";
    public override int LevelRequirement => 86;
    public override bool ConsumesStep => false;
    protected override int BaseCPCost => 0;
    protected override int BuffDuration => 0;

    public override string? GetFailReason(
        CraftingState state, CrafterStats stats, RecipeParameters recipe)
    {
        if (!stats.IsSpecialist)
            return "Requires specialist";
        if (state.HeartAndSoulUsed)
            return "Heart and Soul can only be used once per craft";
        return base.GetFailReason(state, stats, recipe);
    }

    protected override CraftingState ApplyEffect(
        CraftingState state, CrafterStats stats, RecipeParameters recipe)
    {
        return state with
        {
            HeartAndSoulActive = true,
            HeartAndSoulUsed = true,
        };
    }
}
