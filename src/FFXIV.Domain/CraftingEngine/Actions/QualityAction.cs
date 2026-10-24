using FFXIV.Domain.CraftingEngine.Models;

namespace FFXIV.Domain.CraftingEngine.Actions;

public abstract class QualityAction : CraftingActionBase
{
    public override ActionCategory Category => ActionCategory.Quality;
    protected override int BaseDurabilityCost => 10;
    protected override int BaseSuccessRate => 100;

    protected abstract int Potency { get; }

    protected override CraftingState ApplyEffect(
        CraftingState state, CrafterStats stats, RecipeParameters recipe)
    {
        var qualityIncrease = CalculateQuality(state, stats, recipe, Potency);
        var newQuality = Math.Min(state.Quality + qualityIncrease, state.MaxQuality);

        return state with
        {
            Quality = newQuality,
            GreatStridesTurns = 0,
            InnerQuietStacks = GainInnerQuiet(state.InnerQuietStacks, GetInnerQuietGain(), stats),
        };
    }

    protected virtual int GetInnerQuietGain() => 1;

    // Inner Quiet is a level 11 trait (Raphael raphael-sim/src/state.rs:120; Teamcraft quality-action.ts:96).
    protected internal static int GainInnerQuiet(int stacks, int gain, CrafterStats stats) =>
        stats.Level >= 11 ? Math.Min(stacks + gain, 10) : stacks;

    // Buffs and Inner Quiet multiply the potency; the condition counts in halves inside the numerator so the
    // gain truncates once (Raphael raphael-sim/src/effects.rs:76-80, actions.rs:28-52; Teamcraft quality-action.ts:65-94).
    protected internal static int CalculateQuality(
        CraftingState state, CrafterStats stats, RecipeParameters recipe, int potency)
    {
        var baseQuality = FormulaCalculator.CalculateBaseQuality(
            stats.GetEffectiveControl(state.InnerQuietStacks), recipe.QualityDivider, recipe.QualityModifier,
            stats.Level, recipe.ClassJobLevel);

        var buffModifier = 10;
        if (state.GreatStridesTurns > 0)
            buffModifier += 10;
        if (state.InnovationTurns > 0)
            buffModifier += 5;

        var conditionHalves = state.Condition switch
        {
            CraftCondition.Good => 3,
            CraftCondition.Excellent => 8,
            CraftCondition.Poor => 1,
            _ => 2,
        };

        var innerQuietModifier = (state.InnerQuietStacks + 10) * buffModifier;
        return (int)((long)baseQuality * potency * innerQuietModifier * conditionHalves / 20000);
    }
}
