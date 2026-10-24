using FFXIV.Domain.CraftingEngine.Models;
using FFXIV.Domain.Entities;
using FFXIV.Domain.Enums;

namespace FFXIV.Domain.Utilities;

public sealed record ConsumableStatBonuses(int Craftsmanship, int Control, int CP)
{
    public static readonly ConsumableStatBonuses None = new(0, 0, 0);
}

// The reference for the meal and medicine rule the frontend applies; goldens/consumable-stats.json pins the TypeScript port to it.
public static class ConsumableStats
{
    // Integer product before the division, as Raphael computes it; a float percentage (Teamcraft's
    // form) can floor one lower.
    public static int Bonus(int baseValue, ConsumableBonus bonus, bool hq)
    {
        var (value, max) = hq ? (bonus.ValueHQ, bonus.MaxHQ) : (bonus.Value, bonus.Max);
        return bonus.IsRelative ? Math.Min(max, baseValue * value / 100) : value;
    }

    public static ConsumableStatBonuses BonusesFor(CrafterStats baseStats, IEnumerable<ConsumableBonus> bonuses, bool hq)
    {
        int craftsmanship = 0, control = 0, cp = 0;
        foreach (var bonus in bonuses)
        {
            switch (bonus.Stat)
            {
                case ConsumableStat.Craftsmanship:
                    craftsmanship += Bonus(baseStats.Craftsmanship, bonus, hq);
                    break;
                case ConsumableStat.Control:
                    control += Bonus(baseStats.Control, bonus, hq);
                    break;
                case ConsumableStat.CP:
                    cp += Bonus(baseStats.CP, bonus, hq);
                    break;
            }
        }

        return new ConsumableStatBonuses(craftsmanship, control, cp);
    }

    // Meal and medicine each scale off the base stat and cap on their own; neither sees the other's boost.
    public static CrafterStats Apply(
        CrafterStats baseStats,
        IEnumerable<ConsumableBonus>? meal,
        bool mealHq,
        IEnumerable<ConsumableBonus>? medicine,
        bool medicineHq)
    {
        var mealBonus = meal is null ? ConsumableStatBonuses.None : BonusesFor(baseStats, meal, mealHq);
        var medicineBonus = medicine is null ? ConsumableStatBonuses.None : BonusesFor(baseStats, medicine, medicineHq);

        return baseStats with
        {
            Craftsmanship = baseStats.Craftsmanship + mealBonus.Craftsmanship + medicineBonus.Craftsmanship,
            Control = baseStats.Control + mealBonus.Control + medicineBonus.Control,
            CP = baseStats.CP + mealBonus.CP + medicineBonus.CP,
        };
    }
}
