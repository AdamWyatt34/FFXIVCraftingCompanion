using FFXIV.Domain.Enums;

namespace FFXIV.Domain.Entities;

public class ConsumableBonus
{
    public int ItemId { get; set; }
    public ConsumableKind Kind { get; set; }
    public ConsumableStat Stat { get; set; }

    // A relative row adds Value percent of the base stat, capped at Max; any other row adds Value flat.
    public bool IsRelative { get; set; }
    public int Value { get; set; }
    public int Max { get; set; }
    public int ValueHQ { get; set; }
    public int MaxHQ { get; set; }

    public Item Item { get; set; } = null!;
}
