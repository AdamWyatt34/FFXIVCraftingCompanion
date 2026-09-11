using FFXIV.Domain.CraftingEngine.Models;

namespace FFXIV.Domain.CraftingEngine.Actions.Progress;

public class FocusedSynthesis : ProgressAction
{
    public override int Id => 100235;
    public override string Name => "FocusedSynthesis";
    public override string InGameName => "Focused Synthesis";
    public override int LevelRequirement => 67;
    protected override int BaseCPCost => 5;
    protected override int BaseSuccessRate => 50;
    protected override int GetPotency(CraftingState state, CrafterStats stats) => 200;

    public override int GetSuccessRate(CraftingState state)
    {
        if (state.ObserveActive)
            return 100;
        return base.GetSuccessRate(state);
    }
}
