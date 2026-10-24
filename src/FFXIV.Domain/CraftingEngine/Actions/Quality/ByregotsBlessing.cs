using FFXIV.Domain.CraftingEngine.Models;

namespace FFXIV.Domain.CraftingEngine.Actions.Quality;

public class ByregotsBlessing : QualityAction
{
    public override int Id => 100339;
    public override string Name => "ByregotsBlessing";
    public override string InGameName => "Byregot's Blessing";
    public override int LevelRequirement => 50;
    protected override int BaseCPCost => 24;
    protected override int Potency => 100; // base, overridden dynamically

    public override string? GetFailReason(
        CraftingState state, CrafterStats stats, RecipeParameters recipe)
    {
        if (state.InnerQuietStacks <= 0)
            return "Requires Inner Quiet stacks";
        return base.GetFailReason(state, stats, recipe);
    }

    protected override CraftingState ApplyEffect(
        CraftingState state, CrafterStats stats, RecipeParameters recipe)
    {
        var effectivePotency = 100 + (state.InnerQuietStacks * 20);
        var qualityIncrease = CalculateQuality(state, stats, recipe, effectivePotency);

        return state with
        {
            Quality = Math.Min(state.Quality + qualityIncrease, state.MaxQuality),
            GreatStridesTurns = 0,
            InnerQuietStacks = 0,
        };
    }
}
