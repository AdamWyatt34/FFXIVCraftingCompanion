namespace FFXIV.Domain.Models;

public record CrafterStats
{
    public int Level { get; init; }
    public int Craftsmanship { get; init; }
    public int Control { get; init; }
    public int CP { get; init; }
    public bool IsSpecialist { get; init; } = false;

}
