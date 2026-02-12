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
        var qualityIncrease = CalculateQuality(state, stats, recipe);
        var newQuality = Math.Min(state.Quality + qualityIncrease, state.MaxQuality);

        return state with
        {
            Quality = newQuality,
            GreatStridesTurns = 0,
            InnerQuietStacks = Math.Min(state.InnerQuietStacks + GetInnerQuietGain(), 10),
        };
    }

    protected virtual int GetInnerQuietGain() => 1;

    protected int CalculateQuality(CraftingState state, CrafterStats stats, RecipeParameters recipe)
    {
        var effectiveControl = stats.GetEffectiveControl(state.InnerQuietStacks);

        var baseQuality = FormulaCalculator.CalculateBaseQuality(
            effectiveControl, recipe.QualityDivider, recipe.QualityModifier,
            stats.Level, recipe.RecipeLevel);

        var efficiency = Potency;

        if (state.InnovationTurns > 0)
            efficiency += 50;

        if (state.GreatStridesTurns > 0)
            efficiency += 100;

        var qualityIncrease = (int)(baseQuality * efficiency / 100.0);

        qualityIncrease = state.Condition switch
        {
            CraftCondition.Good => (int)(qualityIncrease * 1.5),
            CraftCondition.Excellent => qualityIncrease * 4,
            CraftCondition.Poor => (int)(qualityIncrease * 0.5),
            _ => qualityIncrease,
        };

        return qualityIncrease;
    }
}
