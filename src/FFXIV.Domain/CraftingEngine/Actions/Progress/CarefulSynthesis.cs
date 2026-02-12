namespace FFXIV.Domain.CraftingEngine.Actions.Progress;

public class CarefulSynthesis : ProgressAction
{
    public override int Id => 100203;
    public override string Name => "CarefulSynthesis";
    public override string InGameName => "Careful Synthesis";
    public override int LevelRequirement => 62;
    protected override int BaseCPCost => 7;
    protected override int Potency => 180;
}
