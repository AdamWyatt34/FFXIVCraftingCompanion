namespace FFXIV.Domain.CraftingEngine.Actions.Quality;

public class PreparatoryTouch : QualityAction
{
    public override int Id => 100299;
    public override string Name => "PreparatoryTouch";
    public override string InGameName => "Preparatory Touch";
    public override int LevelRequirement => 71;
    protected override int BaseCPCost => 40;
    protected override int BaseDurabilityCost => 20;
    protected override int Potency => 200;

    protected override int GetInnerQuietGain() => 2;
}
