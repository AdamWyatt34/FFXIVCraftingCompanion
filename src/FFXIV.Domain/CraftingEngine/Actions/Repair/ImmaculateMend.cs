using FFXIV.Domain.CraftingEngine.Models;

namespace FFXIV.Domain.CraftingEngine.Actions.Repair;

public class ImmaculateMend : CraftingActionBase
{
    public override int Id => 100439;
    public override string Name => "ImmaculateMend";
    public override string InGameName => "Immaculate Mend";
    public override int LevelRequirement => 98;
    public override ActionCategory Category => ActionCategory.Repair;
    protected override int BaseCPCost => 112;
    protected override int BaseDurabilityCost => 0;
    protected override int BaseSuccessRate => 100;

    protected override CraftingState ApplyEffect(
        CraftingState state, CrafterStats stats, RecipeParameters recipe)
    {
        return state with
        {
            Durability = state.MaxDurability,
        };
    }
}
