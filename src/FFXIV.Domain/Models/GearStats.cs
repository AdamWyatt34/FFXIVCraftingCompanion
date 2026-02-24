namespace FFXIV.Domain.Models;

public class GearStats
{
    // DoH stats
    public int Craftsmanship { get; set; }
    public int Control { get; set; }
    public int CP { get; set; }

    // DoL stats
    public int Gathering { get; set; }
    public int Perception { get; set; }
    public int GP { get; set; }

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

    public double TotalWeightedScore { get; set; }
}
