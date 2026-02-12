using FFXIV.Domain.CraftingEngine.Models;

namespace FFXIV.Domain.CraftingEngine.Actions.Quality;

public class AdvancedTouch : QualityAction
{
    public const int ActionId = 100411;

    public override int Id => ActionId;
    public override string Name => "AdvancedTouch";
    public override string InGameName => "Advanced Touch";
    public override int LevelRequirement => 68;
    protected override int BaseCPCost => 46;
    protected override int Potency => 150;

    public override int GetCPCost(CraftingState state)
    {
        var baseCost = state.ComboAction == StandardTouch.ActionId ? 18 : 46;
        if (state.Condition == CraftCondition.Pliant)
            return (int)Math.Ceiling(baseCost / 2.0);
        return baseCost;
    }
}
