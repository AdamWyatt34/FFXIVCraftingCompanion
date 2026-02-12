using FFXIV.Domain.CraftingEngine.Models;

namespace FFXIV.Domain.CraftingEngine.Actions;

public abstract class CraftingActionBase : ICraftingAction
{
    public abstract int Id { get; }
    public abstract string Name { get; }
    public abstract string InGameName { get; }
    public abstract int LevelRequirement { get; }
    public abstract ActionCategory Category { get; }

    protected abstract int BaseCPCost { get; }
    protected abstract int BaseDurabilityCost { get; }
    protected abstract int BaseSuccessRate { get; }

    protected abstract CraftingState ApplyEffect(
        CraftingState state, CrafterStats stats, RecipeParameters recipe);

    public virtual int GetCPCost(CraftingState state)
    {
        var cost = BaseCPCost;
        if (state.Condition == CraftCondition.Pliant)
            cost = (int)Math.Ceiling(cost / 2.0);
        return cost;
    }

    public virtual int GetDurabilityCost(CraftingState state)
    {
        if (BaseDurabilityCost == 0) return 0;
        if (state.TrainedPerfectionActive) return 0;

        var cost = (double)BaseDurabilityCost;

        if (state.WasteNotTurns > 0 || state.WasteNotIITurns > 0)
            cost /= 2.0;

        if (state.Condition == CraftCondition.Sturdy)
            cost /= 2.0;

        return (int)Math.Ceiling(cost);
    }

    public virtual int GetSuccessRate(CraftingState state)
    {
        var rate = BaseSuccessRate;
        if (state.Condition == CraftCondition.Centered)
            rate += 25;
        return Math.Min(rate, 100);
    }

    protected virtual int GetComboAction() => 0;
    protected virtual bool GetObserveActive() => false;

    public virtual bool CanExecute(CraftingState state, CrafterStats stats, RecipeParameters recipe)
    {
        return GetFailReason(state, stats, recipe) is null;
    }

    public virtual string? GetFailReason(CraftingState state, CrafterStats stats, RecipeParameters recipe)
    {
        if (state.Status != CraftStatus.InProgress)
            return "Craft is not in progress";
        if (stats.Level < LevelRequirement)
            return $"Requires level {LevelRequirement}";
        if (state.CP < GetCPCost(state))
            return "Not enough CP";
        return null;
    }

    public virtual CraftingState Execute(
        CraftingState state, CrafterStats stats, RecipeParameters recipe, bool forceSuccess = true)
    {
        // 1. Deduct CP
        state = state with { CP = state.CP - GetCPCost(state) };

        // 2. Apply action-specific effects (only on success)
        if (forceSuccess)
            state = ApplyEffect(state, stats, recipe);

        // 3. Deduct durability
        state = state with { Durability = state.Durability - GetDurabilityCost(state) };

        // 4. Consume Trained Perfection if it was active and action has durability cost
        if (state.TrainedPerfectionActive && BaseDurabilityCost > 0)
            state = state with { TrainedPerfectionActive = false };

        // 5. Capture Manipulation turns before tick (heals for full duration)
        var manipTurnsBeforeTick = state.ManipulationTurns;

        // 6. Tick buffs
        state = state.WithBuffTick();

        // 7. Manipulation heal (+5 durability if was active and item isn't broken)
        if (manipTurnsBeforeTick > 0 && state.Durability > 0)
            state = state with { Durability = Math.Min(state.MaxDurability, state.Durability + 5) };

        // 8. Update step counter and combo state (combo broken on failure)
        state = state with
        {
            Step = state.Step + 1,
            IsFirstStep = false,
            ComboAction = forceSuccess ? GetComboAction() : 0,
            ObserveActive = forceSuccess ? GetObserveActive() : false,
        };

        // 9. Determine status
        if (state.Progress >= state.MaxProgress)
            state = state with { Status = CraftStatus.Completed };
        else if (state.Durability <= 0)
            state = state with { Status = CraftStatus.Failed };

        return state;
    }
}
