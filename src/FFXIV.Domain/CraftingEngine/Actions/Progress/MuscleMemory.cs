using FFXIV.Domain.CraftingEngine.Models;

namespace FFXIV.Domain.CraftingEngine.Actions.Progress;

public class MuscleMemory : ProgressAction
{
    public override int Id => 100379;
    public override string Name => "MuscleMemory";
    public override string InGameName => "Muscle Memory";
    public override int LevelRequirement => 54;
    protected override int BaseCPCost => 6;
    protected override int GetPotency(CraftingState state, CrafterStats stats) => 300;

    public override string? GetFailReason(
        CraftingState state, CrafterStats stats, RecipeParameters recipe)
    {
        if (!state.IsFirstStep)
            return "Can only be used as the first action";
        return base.GetFailReason(state, stats, recipe);
    }

    protected override CraftingState ApplyGrantedBuffs(CraftingState state)
        => state with { MuscleMemoryTurns = state.Condition == CraftCondition.Primed ? 7 : 5 };
}
