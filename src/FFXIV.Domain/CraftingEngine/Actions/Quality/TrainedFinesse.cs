using FFXIV.Domain.CraftingEngine.Models;

namespace FFXIV.Domain.CraftingEngine.Actions.Quality;

public class TrainedFinesse : QualityAction
{
    public override int Id => 100435;
    public override string Name => "TrainedFinesse";
    public override string InGameName => "Trained Finesse";
    public override int LevelRequirement => 90;
    protected override int BaseCPCost => 32;
    protected override int BaseDurabilityCost => 0;
    protected override int Potency => 100;

    public override string? GetFailReason(
        CraftingState state, CrafterStats stats, RecipeParameters recipe)
    {
        if (state.InnerQuietStacks < 10)
            return "Requires 10 Inner Quiet stacks";
        return base.GetFailReason(state, stats, recipe);
    }
}
