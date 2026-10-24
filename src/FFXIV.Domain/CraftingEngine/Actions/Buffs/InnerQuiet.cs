using FFXIV.Domain.CraftingEngine.Models;

namespace FFXIV.Domain.CraftingEngine.Actions.Buffs;

public class InnerQuiet : BuffAction
{
    public override int Id => 100252;
    public override string Name => "InnerQuiet";
    public override string InGameName => "Inner Quiet";
    public override int LevelRequirement => 11;

    // Inner Quiet is a trait that stacks on its own from level 11; neither Raphael nor Teamcraft has the action.
    public override bool IsLegacy => true;
    protected override int BaseCPCost => 18;
    protected override int BuffDuration => 0;

    protected override CraftingState ApplyEffect(
        CraftingState state, CrafterStats stats, RecipeParameters recipe)
    {
        return state;
    }
}
