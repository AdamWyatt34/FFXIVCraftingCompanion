namespace FFXIV.Domain.CraftingEngine.Actions.Quality;

public class FocusedTouch : QualityAction
{
    public override int Id => 100243;
    public override string Name => "FocusedTouch";
    public override string InGameName => "Focused Touch";
    public override int LevelRequirement => 68;

    // Removed from the game in patch 7.0; neither Raphael nor Teamcraft has it.
    public override bool IsLegacy => true;
    protected override int BaseCPCost => 18;
    protected override int BaseSuccessRate => 50;
    protected override int Potency => 150;
}
