using FFXIV.Domain.CraftingEngine.Models;

namespace FFXIV.Domain.CraftingEngine.Actions.Quality;

public class FocusedTouch : QualityAction
{
    public override int Id => 100243;
    public override string Name => "FocusedTouch";
    public override string InGameName => "Focused Touch";
    public override int LevelRequirement => 68;
    protected override int BaseCPCost => 18;
    protected override int BaseSuccessRate => 50;
    protected override int Potency => 150;

    public override int GetSuccessRate(CraftingState state)
    {
        if (state.ObserveActive)
            return 100;
        return base.GetSuccessRate(state);
    }
}
