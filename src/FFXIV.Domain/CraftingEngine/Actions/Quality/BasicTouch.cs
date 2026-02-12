namespace FFXIV.Domain.CraftingEngine.Actions.Quality;

public class BasicTouch : QualityAction
{
    public const int ActionId = 100002;

    public override int Id => ActionId;
    public override string Name => "BasicTouch";
    public override string InGameName => "Basic Touch";
    public override int LevelRequirement => 5;
    protected override int BaseCPCost => 18;
    protected override int Potency => 100;

    protected override int GetComboAction() => ActionId;
}
