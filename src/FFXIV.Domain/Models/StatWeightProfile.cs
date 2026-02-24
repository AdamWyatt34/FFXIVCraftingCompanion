namespace FFXIV.Domain.Models;

public class StatWeightProfile
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    // DoH stats
    public double CraftsmanshipWeight { get; set; }
    public double ControlWeight { get; set; }
    public double CpWeight { get; set; }

    // DoL stats
    public double GatheringWeight { get; set; }
    public double PerceptionWeight { get; set; }
    public double GpWeight { get; set; }

    // Combat stats
    public double VitalityWeight { get; set; }
    public double StrengthWeight { get; set; }
    public double DexterityWeight { get; set; }
    public double IntelligenceWeight { get; set; }
    public double MindWeight { get; set; }
    public double PietyWeight { get; set; }
    public double TenacityWeight { get; set; }
    public double DirectHitWeight { get; set; }
    public double CriticalHitWeight { get; set; }
    public double DeterminationWeight { get; set; }
    public double SkillSpeedWeight { get; set; }
    public double SpellSpeedWeight { get; set; }
    public double PhysicalDamageWeight { get; set; }
    public double DefenseWeight { get; set; }
    public double MagicDefenseWeight { get; set; }
}
