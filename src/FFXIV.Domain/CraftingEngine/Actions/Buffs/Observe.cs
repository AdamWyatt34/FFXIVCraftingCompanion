using FFXIV.Domain.CraftingEngine.Models;

namespace FFXIV.Domain.CraftingEngine.Actions.Buffs;

public class Observe : BuffAction
{
    public override int Id => 100010;
    public override string Name => "Observe";
    public override string InGameName => "Observe";
    public override int LevelRequirement => 13;
    protected override int BaseCPCost => 7;
    protected override int BuffDuration => 0;

    protected override bool GetObserveActive() => true;

    protected override CraftingState ApplyEffect(
        CraftingState state, CrafterStats stats, RecipeParameters recipe)
    {
        return state;
    }
}
