using FFXIV.Domain.Enums;

namespace FFXIV.Domain.Entities;

public class Item
{
    public int ItemId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string IconPath { get; set; } = string.Empty;
    public int ItemLevel { get; set; }
    public ItemCategory Category { get; set; }
    public bool CanBeHQ { get; set; }
}
