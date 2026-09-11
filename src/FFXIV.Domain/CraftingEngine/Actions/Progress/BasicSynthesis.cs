using FFXIV.Domain.CraftingEngine.Models;

namespace FFXIV.Domain.CraftingEngine.Actions.Progress;

public class BasicSynthesis : ProgressAction
{
    public override int Id => 100001;
    public override string Name => "BasicSynthesis";
    public override string InGameName => "Basic Synthesis";
    public override int LevelRequirement => 1;
    protected override int BaseCPCost => 0;
    // Basic Synthesis Mastery (level 31) raises the potency from 100 to 120.
    protected override int GetPotency(CraftingState state, CrafterStats stats) => stats.Level >= 31 ? 120 : 100;
}
