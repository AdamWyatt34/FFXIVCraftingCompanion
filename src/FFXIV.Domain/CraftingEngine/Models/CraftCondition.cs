namespace FFXIV.Domain.CraftingEngine.Models;

public enum CraftCondition
{
    // Standard conditions
    Normal,       // Base rates (default)
    Good,         // 1.5x quality multiplier
    Excellent,    // 4x quality multiplier
    Poor,         // 0.5x quality multiplier (always follows Excellent)

    // Expert recipe conditions (only on expert crafts)
    Centered,     // +25% success rate on next action
    Sturdy,       // 50% durability cost reduction
    Pliant,       // 50% CP cost reduction
    Malleable,    // Guaranteed progress efficiency bonus
    Primed,       // +2 turns to next buff applied
    GoodOmen      // Next step is guaranteed Good
}
