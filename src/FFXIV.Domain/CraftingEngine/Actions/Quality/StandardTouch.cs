using FFXIV.Domain.CraftingEngine.Models;

namespace FFXIV.Domain.CraftingEngine.Actions.Quality;

public class StandardTouch : QualityAction
{
    public const int ActionId = 100004;

    public override int Id => ActionId;
    public override string Name => "StandardTouch";
    public override string InGameName => "Standard Touch";
    public override int LevelRequirement => 18;
    protected override int BaseCPCost => 32;
    protected override int Potency => 125;

    public override int GetCPCost(CraftingState state)
    {
        var baseCost = state.ComboAction == BasicTouch.ActionId ? 18 : 32;
        if (state.Condition == CraftCondition.Pliant)
            return (int)Math.Ceiling(baseCost / 2.0);
        return baseCost;
    }

    protected override int GetComboAction() => ActionId;
}
