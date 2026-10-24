using FFXIV.Domain.CraftingEngine.Models;

namespace FFXIV.Domain.CraftingEngine.Actions;

public abstract class BuffAction : CraftingActionBase
{
    public override ActionCategory Category => ActionCategory.Buff;
    public override int MacroWaitSeconds => 2;
    protected override int BaseDurabilityCost => 0;
    protected override int BaseSuccessRate => 100;

    protected abstract int BuffDuration { get; }

    // No-op for buffs that are flags rather than timers.
    protected virtual CraftingState WithBuffTurns(CraftingState state, int turns) => state;

    // A recast replaces the running buff, so the old one must neither tick nor heal this step.
    protected override CraftingState ApplyEffect(
        CraftingState state, CrafterStats stats, RecipeParameters recipe)
        => WithBuffTurns(state, 0);

    protected override CraftingState ApplyGrantedBuffs(CraftingState state)
        => WithBuffTurns(state, GetEffectiveDuration(state));

    protected int GetEffectiveDuration(CraftingState state)
    {
        var duration = BuffDuration;
        if (state.Condition == CraftCondition.Primed)
            duration += 2;
        return duration;
    }
}
