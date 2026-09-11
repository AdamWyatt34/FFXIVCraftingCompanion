using FFXIV.Domain.CraftingEngine.Models;

namespace FFXIV.Domain.CraftingEngine.Actions.Other;

public class CarefulObservation : CraftingActionBase
{
    public override int Id => 100395;
    public override string Name => "CarefulObservation";
    public override string InGameName => "Careful Observation";
    public override int LevelRequirement => 55;
    public override ActionCategory Category => ActionCategory.Other;
    public override bool ConsumesStep => false;
    public override bool AdvancesCondition => true;
    protected override int BaseCPCost => 0;
    protected override int BaseDurabilityCost => 0;
    protected override int BaseSuccessRate => 100;

    public override string? GetFailReason(
        CraftingState state, CrafterStats stats, RecipeParameters recipe)
    {
        if (!stats.IsSpecialist)
            return "Requires specialist";
        if (state.CarefulObservationCount >= 3)
            return "Careful Observation can only be used 3 times per craft";
        return base.GetFailReason(state, stats, recipe);
    }

    protected override CraftingState ApplyEffect(
        CraftingState state, CrafterStats stats, RecipeParameters recipe)
    {
        return state with
        {
            CarefulObservationCount = state.CarefulObservationCount + 1,
        };
    }
}
