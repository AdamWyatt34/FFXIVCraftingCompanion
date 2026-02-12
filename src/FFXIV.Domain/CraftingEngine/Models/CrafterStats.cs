namespace FFXIV.Domain.CraftingEngine.Models;

public record CrafterStats
{
    public int Level { get; init; }
    public int Craftsmanship { get; init; }
    public int Control { get; init; }
    public int CP { get; init; }
    public bool IsSpecialist { get; init; } = false;

    // Computed: effective control with Inner Quiet stacks
    public int GetEffectiveControl(int innerQuietStacks)
        => (int)(Control * (1.0 + 0.1 * innerQuietStacks));
}
