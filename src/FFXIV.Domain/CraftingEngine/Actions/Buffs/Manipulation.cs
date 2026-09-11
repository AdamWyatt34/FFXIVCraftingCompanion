using FFXIV.Domain.CraftingEngine.Models;

namespace FFXIV.Domain.CraftingEngine.Actions.Buffs;

public class Manipulation : BuffAction
{
    public override int Id => 100278;
    public override string Name => "Manipulation";
    public override string InGameName => "Manipulation";
    public override int LevelRequirement => 65;
    protected override int BaseCPCost => 96;
    protected override int BuffDuration => 8;

    protected override CraftingState WithBuffTurns(CraftingState state, int turns)
        => state with { ManipulationTurns = turns };
}
