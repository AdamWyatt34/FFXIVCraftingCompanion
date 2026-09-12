using FFXIV.Domain.CraftingEngine.Models;

namespace FFXIV.Domain.CraftingEngine.Actions.Buffs;

public class Innovation : BuffAction
{
    public override int Id => 100209;
    public override string Name => "Innovation";
    public override string InGameName => "Innovation";
    public override int LevelRequirement => 26;
    protected override int BaseCPCost => 18;
    protected override int BuffDuration => 4;

    protected override CraftingState WithBuffTurns(CraftingState state, int turns)
        => state with { InnovationTurns = turns };
}
