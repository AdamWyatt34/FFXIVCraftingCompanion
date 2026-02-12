using FFXIV.Domain.CraftingEngine.Models;

namespace FFXIV.Domain.CraftingEngine.Actions.Repair;

public class MastersMend : CraftingActionBase
{
    public override int Id => 100003;
    public override string Name => "MastersMend";
    public override string InGameName => "Master's Mend";
    public override int LevelRequirement => 7;
    public override ActionCategory Category => ActionCategory.Repair;
    protected override int BaseCPCost => 88;
    protected override int BaseDurabilityCost => 0;
    protected override int BaseSuccessRate => 100;

    protected override CraftingState ApplyEffect(
        CraftingState state, CrafterStats stats, RecipeParameters recipe)
    {
        return state with
        {
            Durability = Math.Min(state.Durability + 30, state.MaxDurability),
        };
    }
}
