using FFXIV.Domain.CraftingEngine.Models;

namespace FFXIV.Domain.CraftingEngine.Actions.Buffs;

public class WasteNotII : BuffAction
{
    public override int Id => 100285;
    public override string Name => "WasteNotII";
    public override string InGameName => "Waste Not II";
    public override int LevelRequirement => 47;
    protected override int BaseCPCost => 98;
    protected override int BuffDuration => 8;

    protected override CraftingState ApplyEffect(
        CraftingState state, CrafterStats stats, RecipeParameters recipe)
    {
        return state with { WasteNotIITurns = GetEffectiveDuration(state) };
    }
}
