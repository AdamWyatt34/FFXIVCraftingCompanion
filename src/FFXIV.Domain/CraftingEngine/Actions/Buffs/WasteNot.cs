using FFXIV.Domain.CraftingEngine.Models;

namespace FFXIV.Domain.CraftingEngine.Actions.Buffs;

public class WasteNot : BuffAction
{
    public override int Id => 100279;
    public override string Name => "WasteNot";
    public override string InGameName => "Waste Not";
    public override int LevelRequirement => 15;
    protected override int BaseCPCost => 56;
    protected override int BuffDuration => 4;

    protected override CraftingState WithBuffTurns(CraftingState state, int turns)
        => state with { WasteNotTurns = turns };
}
