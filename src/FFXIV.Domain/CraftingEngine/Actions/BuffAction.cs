using FFXIV.Domain.CraftingEngine.Models;

namespace FFXIV.Domain.CraftingEngine.Actions;

public abstract class BuffAction : CraftingActionBase
{
    public override ActionCategory Category => ActionCategory.Buff;
    protected override int BaseDurabilityCost => 0;
    protected override int BaseSuccessRate => 100;

    protected abstract int BuffDuration { get; }

    protected int GetEffectiveDuration(CraftingState state)
    {
        var duration = BuffDuration;
        if (state.Condition == CraftCondition.Primed)
            duration += 2;
        return duration;
    }
}
