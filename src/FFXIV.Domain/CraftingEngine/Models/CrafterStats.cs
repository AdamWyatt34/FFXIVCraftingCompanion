namespace FFXIV.Domain.CraftingEngine.Models;

public record CrafterStats
{
    public int Level { get; init; }
    public int Craftsmanship { get; init; }
    public int Control { get; init; }
    public int CP { get; init; }
    public bool IsSpecialist { get; init; } = false;

    // Inner Quiet scales the quality multiplier, not Control: scaling Control would skip base quality's
    // +35 term (Raphael raphael-sim/src/effects.rs:76-80; Teamcraft quality-action.ts:79-90).
    public int GetEffectiveControl(int innerQuietStacks) => Control;
}
