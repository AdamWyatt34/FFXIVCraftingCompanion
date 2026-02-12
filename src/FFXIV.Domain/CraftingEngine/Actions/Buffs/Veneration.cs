using FFXIV.Domain.CraftingEngine.Models;

namespace FFXIV.Domain.CraftingEngine.Actions.Buffs;

public class Veneration : BuffAction
{
    public override int Id => 100297;
    public override string Name => "Veneration";
    public override string InGameName => "Veneration";
    public override int LevelRequirement => 15;
    protected override int BaseCPCost => 18;
    protected override int BuffDuration => 4;

    protected override CraftingState ApplyEffect(
        CraftingState state, CrafterStats stats, RecipeParameters recipe)
    {
        return state with { VenerationTurns = GetEffectiveDuration(state) };
    }
}
