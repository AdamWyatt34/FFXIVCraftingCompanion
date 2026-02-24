using FFXIV.Domain.Models;

namespace FFXIV.Domain.Constants;

public static class StatWeightProfiles
{
    public static readonly StatWeightProfile Balanced = new()
    {
        Name = "Balanced",
        Description = "Well-rounded for general crafting. Slightly favors CP for rotation flexibility.",
        CraftsmanshipWeight = 1.0,
        ControlWeight = 1.0,
        CpWeight = 1.2
    };

    public static readonly StatWeightProfile ProgressFocused = new()
    {
        Name = "ProgressFocused",
        Description = "Prioritizes Craftsmanship for reliably completing difficult crafts.",
        CraftsmanshipWeight = 1.5,
        ControlWeight = 0.7,
        CpWeight = 1.0
    };

    public static readonly StatWeightProfile QualityFocused = new()
    {
        Name = "QualityFocused",
        Description = "Prioritizes Control for maximizing HQ chance on quality-sensitive crafts.",
        CraftsmanshipWeight = 0.7,
        ControlWeight = 1.5,
        CpWeight = 1.0
    };

    public static readonly StatWeightProfile CpFocused = new()
    {
        Name = "CpFocused",
        Description = "Maximizes CP for complex expert rotations that need many actions.",
        CraftsmanshipWeight = 0.8,
        ControlWeight = 0.8,
        CpWeight = 2.0
    };

    public static readonly StatWeightProfile GatheringBalanced = new()
    {
        Name = "GatheringBalanced",
        Description = "Balanced gathering stats with GP emphasis for ability usage.",
        GatheringWeight = 1.0,
        PerceptionWeight = 1.0,
        GpWeight = 1.2
    };

    public static readonly StatWeightProfile PerceptionFocused = new()
    {
        Name = "PerceptionFocused",
        Description = "Maximizes collectability for collectables and aetherial reduction.",
        GatheringWeight = 0.7,
        PerceptionWeight = 1.5,
        GpWeight = 1.0
    };

    // === Combat profiles ===

    public static readonly StatWeightProfile TankBalanced = new()
    {
        Name = "TankBalanced",
        Description = "Balanced tank gear: weapon damage and vitality priority, with defensive substats.",
        PhysicalDamageWeight = 5.0,
        VitalityWeight = 2.0,
        DefenseWeight = 1.5,
        MagicDefenseWeight = 1.0,
        StrengthWeight = 1.5,
        CriticalHitWeight = 1.2,
        DeterminationWeight = 1.0,
        TenacityWeight = 0.8,
        DirectHitWeight = 0.6,
        SkillSpeedWeight = 0.4
    };

    public static readonly StatWeightProfile HealerBalanced = new()
    {
        Name = "HealerBalanced",
        Description = "Balanced healer gear: Mind and Critical Hit priority for strong heals and DPS.",
        PhysicalDamageWeight = 5.0,
        MindWeight = 2.0,
        VitalityWeight = 1.0,
        CriticalHitWeight = 1.5,
        DeterminationWeight = 1.2,
        PietyWeight = 0.8,
        SpellSpeedWeight = 0.6,
        DirectHitWeight = 0.5
    };

    public static readonly StatWeightProfile MeleeDPSBalanced = new()
    {
        Name = "MeleeDPSBalanced",
        Description = "Balanced melee DPS gear: weapon damage > main stat > crit > direct hit.",
        PhysicalDamageWeight = 5.0,
        StrengthWeight = 2.0,
        DexterityWeight = 2.0,
        CriticalHitWeight = 1.5,
        DirectHitWeight = 1.2,
        DeterminationWeight = 1.0,
        SkillSpeedWeight = 0.5,
        VitalityWeight = 0.3
    };

    public static readonly StatWeightProfile RangedDPSBalanced = new()
    {
        Name = "RangedDPSBalanced",
        Description = "Balanced ranged DPS gear: weapon damage > Dexterity > crit > direct hit.",
        PhysicalDamageWeight = 5.0,
        DexterityWeight = 2.0,
        CriticalHitWeight = 1.5,
        DirectHitWeight = 1.2,
        DeterminationWeight = 1.0,
        SkillSpeedWeight = 0.5,
        VitalityWeight = 0.3
    };

    public static readonly StatWeightProfile MagicDPSBalanced = new()
    {
        Name = "MagicDPSBalanced",
        Description = "Balanced magic DPS gear: weapon damage > Intelligence > crit > direct hit.",
        PhysicalDamageWeight = 5.0,
        IntelligenceWeight = 2.0,
        CriticalHitWeight = 1.5,
        DirectHitWeight = 1.2,
        DeterminationWeight = 1.0,
        SpellSpeedWeight = 0.5,
        VitalityWeight = 0.3
    };

    public static readonly IReadOnlyList<StatWeightProfile> DoHProfiles =
        [Balanced, ProgressFocused, QualityFocused, CpFocused];

    public static readonly IReadOnlyList<StatWeightProfile> DoLProfiles =
        [GatheringBalanced, PerceptionFocused];

    public static readonly IReadOnlyList<StatWeightProfile> TankProfiles = [TankBalanced];
    public static readonly IReadOnlyList<StatWeightProfile> HealerProfiles = [HealerBalanced];
    public static readonly IReadOnlyList<StatWeightProfile> MeleeDPSProfiles = [MeleeDPSBalanced];
    public static readonly IReadOnlyList<StatWeightProfile> RangedDPSProfiles = [RangedDPSBalanced];
    public static readonly IReadOnlyList<StatWeightProfile> MagicDPSProfiles = [MagicDPSBalanced];

    private static readonly Dictionary<string, StatWeightProfile> AllProfiles =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Balanced"] = Balanced,
            ["ProgressFocused"] = ProgressFocused,
            ["QualityFocused"] = QualityFocused,
            ["CpFocused"] = CpFocused,
            ["GatheringBalanced"] = GatheringBalanced,
            ["PerceptionFocused"] = PerceptionFocused,
            ["TankBalanced"] = TankBalanced,
            ["HealerBalanced"] = HealerBalanced,
            ["MeleeDPSBalanced"] = MeleeDPSBalanced,
            ["RangedDPSBalanced"] = RangedDPSBalanced,
            ["MagicDPSBalanced"] = MagicDPSBalanced
        };

    public static StatWeightProfile GetProfile(string name) =>
        AllProfiles.GetValueOrDefault(name, Balanced);

    public static IReadOnlyList<StatWeightProfile> GetProfilesForRole(CombatRole role) => role switch
    {
        CombatRole.Tank => TankProfiles,
        CombatRole.Healer => HealerProfiles,
        CombatRole.MeleeDPS => MeleeDPSProfiles,
        CombatRole.RangedDPS => RangedDPSProfiles,
        CombatRole.MagicDPS => MagicDPSProfiles,
        _ => [TankBalanced]
    };
}
