using FFXIV.Domain.Entities;
using FFXIV.Domain.Enums;

namespace FFXIV.Tests.Gear;

internal static class EquipmentTestDataBuilder
{
    public static Equipment CreateDoHEquipment(
        int id, string name, EquipSlot slot,
        int craftsmanship = 0, int control = 0, int cp = 0,
        int equipLevel = 90, int itemLevel = 620,
        string classJobCategory = "CRP,BSM,ARM,GSM,LTW,WVR,ALC,CUL",
        bool canBeHq = true,
        ItemSourceType? sourceType = null,
        int? recipeId = null)
    {
        return new Equipment
        {
            EquipmentId = id,
            Name = name,
            EquipSlot = slot,
            ClassJobCategory = classJobCategory,
            EquipLevel = equipLevel,
            ItemLevel = itemLevel,
            Craftsmanship = craftsmanship,
            Control = control,
            CP = cp,
            MateriaSlots = 2,
            IconPath = $"/icon/{id}.png",
            CanBeHQ = canBeHq,
            Rarity = 2,
            SourceType = sourceType,
            RecipeId = recipeId
        };
    }

    public static Equipment CreateCombatEquipment(
        int id, string name, EquipSlot slot,
        int vitality = 0, int strength = 0, int dexterity = 0,
        int intelligence = 0, int mind = 0,
        int criticalHit = 0, int determination = 0, int directHitRate = 0,
        int physicalDamage = 0, int defense = 0, int magicDefense = 0,
        int equipLevel = 100, int itemLevel = 710,
        string classJobCategory = "PLD,WAR,DRK,GNB",
        bool canBeHq = true,
        ItemSourceType? sourceType = null,
        int? recipeId = null)
    {
        return new Equipment
        {
            EquipmentId = id,
            Name = name,
            EquipSlot = slot,
            ClassJobCategory = classJobCategory,
            EquipLevel = equipLevel,
            ItemLevel = itemLevel,
            Vitality = vitality,
            Strength = strength,
            Dexterity = dexterity,
            Intelligence = intelligence,
            Mind = mind,
            CriticalHit = criticalHit,
            Determination = determination,
            DirectHitRate = directHitRate,
            PhysicalDamage = physicalDamage,
            Defense = defense,
            MagicDefense = magicDefense,
            MateriaSlots = 2,
            IconPath = $"/icon/{id}.png",
            CanBeHQ = canBeHq,
            Rarity = 2,
            SourceType = sourceType,
            RecipeId = recipeId
        };
    }

    public static Equipment CreateDoLEquipment(
        int id, string name, EquipSlot slot,
        int gathering = 0, int perception = 0, int gp = 0,
        int equipLevel = 90, int itemLevel = 620,
        string classJobCategory = "MIN,BTN",
        bool canBeHq = true)
    {
        return new Equipment
        {
            EquipmentId = id,
            Name = name,
            EquipSlot = slot,
            ClassJobCategory = classJobCategory,
            EquipLevel = equipLevel,
            ItemLevel = itemLevel,
            Gathering = gathering,
            Perception = perception,
            GP = gp,
            MateriaSlots = 2,
            IconPath = $"/icon/{id}.png",
            CanBeHQ = canBeHq,
            Rarity = 2
        };
    }
}
