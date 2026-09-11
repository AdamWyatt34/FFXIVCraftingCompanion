using FFXIV.Domain.CraftingEngine.Models;

namespace FFXIV.Domain.CraftingEngine.Actions.Progress;

public class CarefulSynthesis : ProgressAction
{
    public override int Id => 100203;
    public override string Name => "CarefulSynthesis";
    public override string InGameName => "Careful Synthesis";
    public override int LevelRequirement => 62;
    protected override int BaseCPCost => 7;
    // Careful Synthesis Mastery, unlocked at level 82.
    protected override int GetPotency(CraftingState state, CrafterStats stats) => stats.Level >= 82 ? 180 : 150;
}
