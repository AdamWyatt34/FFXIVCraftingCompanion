namespace FFXIV.Domain.CraftingEngine.Actions.Progress;

public class BasicSynthesis : ProgressAction
{
    public override int Id => 100001;
    public override string Name => "BasicSynthesis";
    public override string InGameName => "Basic Synthesis";
    public override int LevelRequirement => 1;
    protected override int BaseCPCost => 0;
    protected override int Potency => 120;
}
