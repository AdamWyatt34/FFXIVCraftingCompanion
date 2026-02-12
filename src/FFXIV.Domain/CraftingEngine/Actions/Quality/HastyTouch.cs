namespace FFXIV.Domain.CraftingEngine.Actions.Quality;

public class HastyTouch : QualityAction
{
    public override int Id => 100355;
    public override string Name => "HastyTouch";
    public override string InGameName => "Hasty Touch";
    public override int LevelRequirement => 9;
    protected override int BaseCPCost => 0;
    protected override int BaseSuccessRate => 60;
    protected override int Potency => 100;
}
