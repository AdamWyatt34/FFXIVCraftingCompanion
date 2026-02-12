using FFXIV.Domain.CraftingEngine.Models;

namespace FFXIV.Domain.CraftingEngine.Actions.Buffs;

public class GreatStrides : BuffAction
{
    public override int Id => 100260;
    public override string Name => "GreatStrides";
    public override string InGameName => "Great Strides";
    public override int LevelRequirement => 21;
    protected override int BaseCPCost => 32;
    protected override int BuffDuration => 3;

    protected override CraftingState ApplyEffect(
        CraftingState state, CrafterStats stats, RecipeParameters recipe)
    {
        return state with { GreatStridesTurns = GetEffectiveDuration(state) };
    }
}
