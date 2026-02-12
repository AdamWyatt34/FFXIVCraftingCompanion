namespace FFXIV.Domain.CraftingEngine.Models;

public enum CraftStatus
{
    InProgress,
    Completed,   // Progress >= Difficulty
    Failed       // Durability <= 0 before completion
}
