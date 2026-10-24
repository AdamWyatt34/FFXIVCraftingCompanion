using FFXIV.Domain.CraftingEngine.Models;

namespace FFXIV.Domain.CraftingEngine.Actions.Progress;

public class RapidSynthesis : ProgressAction
{
    public override int Id => 100033;
    public override string Name => "RapidSynthesis";
    public override string InGameName => "Rapid Synthesis";
    public override int LevelRequirement => 9;
    protected override int BaseCPCost => 0;
    protected override int BaseSuccessRate => 50;
    // Rapid Synthesis Mastery, unlocked at level 63.
    protected override int GetPotency(CraftingState state, CrafterStats stats) => stats.Level >= 63 ? 500 : 250;
}
