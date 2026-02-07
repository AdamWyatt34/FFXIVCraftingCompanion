using FFXIV.Domain.Enums;

namespace FFXIV.Domain.Entities;

public class Equipment
{
    public int EquipmentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public EquipSlot EquipSlot { get; set; }
    public string ClassJobCategory { get; set; } = string.Empty;
    public int EquipLevel { get; set; }
    public int ItemLevel { get; set; }
    public int Craftsmanship { get; set; }
    public int Control { get; set; }
    public int CP { get; set; }
    public int Gathering { get; set; }
    public int Perception { get; set; }
    public int GP { get; set; }
    public int MateriaSlots { get; set; }
}
