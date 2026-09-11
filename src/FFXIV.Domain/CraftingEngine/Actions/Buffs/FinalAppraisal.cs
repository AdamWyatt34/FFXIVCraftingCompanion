using FFXIV.Domain.CraftingEngine.Models;

namespace FFXIV.Domain.CraftingEngine.Actions.Buffs;

public class FinalAppraisal : BuffAction
{
    public override int Id => 100364;
    public override string Name => "FinalAppraisal";
    public override string InGameName => "Final Appraisal";
    public override int LevelRequirement => 42;
    public override bool ConsumesStep => false;
    protected override int BaseCPCost => 1;
    protected override int BuffDuration => 5;

    protected override CraftingState WithBuffTurns(CraftingState state, int turns)
        => state with { FinalAppraisalTurns = turns };
}
