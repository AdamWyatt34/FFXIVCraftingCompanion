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
    public string IconPath { get; set; } = string.Empty;
    public bool CanBeHQ { get; set; }

    // Combat stats
    public int Vitality { get; set; }
    public int Strength { get; set; }
    public int Dexterity { get; set; }
    public int Intelligence { get; set; }
    public int Mind { get; set; }
    public int Piety { get; set; }
    public int Tenacity { get; set; }
    public int DirectHitRate { get; set; }
    public int CriticalHit { get; set; }
    public int Determination { get; set; }
    public int SkillSpeed { get; set; }
    public int SpellSpeed { get; set; }
    public int PhysicalDamage { get; set; }
    public int Defense { get; set; }
    public int MagicDefense { get; set; }
    public int? RecipeId { get; set; }
    public ItemSourceType? SourceType { get; set; }
    public string? SourceDetails { get; set; }
    public int Rarity { get; set; }

    public Recipe? Recipe { get; set; }
}
