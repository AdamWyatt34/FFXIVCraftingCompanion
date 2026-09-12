using FFXIV.Domain.CraftingEngine.Models;

namespace FFXIV.Domain.CraftingEngine.Actions.Quality;

public class Reflect : QualityAction
{
    public override int Id => 100387;
    public override string Name => "Reflect";
    public override string InGameName => "Reflect";
    public override int LevelRequirement => 69;
    protected override int BaseCPCost => 6;
    protected override int Potency => 300;

    public override string? GetFailReason(
        CraftingState state, CrafterStats stats, RecipeParameters recipe)
    {
        if (!state.IsFirstStep)
            return "Can only be used as the first action";
        return base.GetFailReason(state, stats, recipe);
    }

    protected override int GetInnerQuietGain() => 2;
}
