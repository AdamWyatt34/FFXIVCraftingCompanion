using FFXIV.Domain.Models;
using FFXIV.Domain.Entities;
using FFXIV.Domain.Enums;
using FFXIV.Domain.Utilities;

namespace FFXIV.Tests.Consumables;

public class ConsumableStatsTests
{
    // Rroneek Steak (44091), as the Item and ItemFood sheets carry it.
    private static readonly ConsumableBonus[] Steak =
    [
        Relative(ConsumableKind.Meal, ConsumableStat.Control, 4, 77, 5, 97),
        Relative(ConsumableKind.Meal, ConsumableStat.CP, 21, 73, 26, 92),
    ];

    private static readonly CrafterStats Base = new() { Level = 100, Craftsmanship = 4900, Control = 4800, CP = 620, IsSpecialist = true };

    [Fact]
    public void Relative_bonus_floors_the_percentage()
    {
        var bonus = Relative(ConsumableKind.Medicine, ConsumableStat.Craftsmanship, 4, 200, 5, 250);

        ConsumableStats.Bonus(3999, bonus, hq: false).Should().Be(159);
    }

    [Fact]
    public void Relative_bonus_stops_at_its_cap()
    {
        var bonus = Relative(ConsumableKind.Meal, ConsumableStat.Control, 4, 77, 5, 97);

        ConsumableStats.Bonus(5000, bonus, hq: false).Should().Be(77);
    }

    [Fact]
    public void Hq_reads_the_hq_value_and_cap()
    {
        var control = Steak[0];

        ConsumableStats.Bonus(1500, control, hq: false).Should().Be(60);
        ConsumableStats.Bonus(1500, control, hq: true).Should().Be(75);
        ConsumableStats.Bonus(4800, control, hq: false).Should().Be(77);
        ConsumableStats.Bonus(4800, control, hq: true).Should().Be(97);
    }

    [Fact]
    public void Non_relative_bonus_adds_its_value_flat()
    {
        var bonus = new ConsumableBonus
        {
            Kind = ConsumableKind.Medicine, Stat = ConsumableStat.CP, IsRelative = false, Value = 21, Max = 0, ValueHQ = 27, MaxHQ = 0,
        };

        ConsumableStats.Bonus(620, bonus, hq: false).Should().Be(21);
        ConsumableStats.Bonus(620, bonus, hq: true).Should().Be(27);
    }

    [Fact]
    public void BonusesFor_sums_per_stat()
    {
        ConsumableStats.BonusesFor(Base, Steak, hq: true).Should().Be(new ConsumableStatBonuses(0, 97, 92));
        ConsumableStats.BonusesFor(Base, Steak, hq: false).Should().Be(new ConsumableStatBonuses(0, 77, 73));
    }

    [Fact]
    public void Meal_and_medicine_each_scale_off_the_base_stat()
    {
        var stats = Base with { CP = 300 };
        var medicine = new[] { Relative(ConsumableKind.Medicine, ConsumableStat.CP, 6, 21, 8, 27) };

        var result = ConsumableStats.Apply(stats, Steak, mealHq: false, medicine, medicineHq: false);

        // 63 from the meal and 18 from the medicine, both off 300; chained, the medicine would add 21.
        result.CP.Should().Be(300 + 63 + 18);
    }

    [Fact]
    public void Apply_adds_both_consumables_and_keeps_level_and_specialist()
    {
        var medicine = new[] { Relative(ConsumableKind.Medicine, ConsumableStat.Craftsmanship, 4, 200, 5, 250) };

        var result = ConsumableStats.Apply(Base, Steak, mealHq: true, medicine, medicineHq: false);

        result.Should().Be(new CrafterStats { Level = 100, Craftsmanship = 4900 + 196, Control = 4800 + 97, CP = 620 + 92, IsSpecialist = true });
    }

    [Fact]
    public void No_consumables_leave_the_base_unchanged()
    {
        ConsumableStats.Apply(Base, null, false, null, false).Should().Be(Base);
    }

    [Fact]
    public void Zero_value_slot_adds_nothing()
    {
        var empty = new[] { Relative(ConsumableKind.Meal, ConsumableStat.Craftsmanship, 0, 0, 0, 0) };

        ConsumableStats.Apply(Base, empty, mealHq: true, null, false).Should().Be(Base);
    }

    private static ConsumableBonus Relative(ConsumableKind kind, ConsumableStat stat, int value, int max, int valueHq, int maxHq) =>
        new() { Kind = kind, Stat = stat, IsRelative = true, Value = value, Max = max, ValueHQ = valueHq, MaxHQ = maxHq };
}
