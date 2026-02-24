namespace FFXIV.Domain.Constants;

public enum CombatRole
{
    Tank,
    Healer,
    MeleeDPS,
    RangedDPS,
    MagicDPS
}

public static class JobClassification
{
    public static readonly string[] DoHJobs = ["CRP", "BSM", "ARM", "GSM", "LTW", "WVR", "ALC", "CUL"];
    public static readonly string[] DoLJobs = ["MIN", "BTN", "FSH"];
    public static readonly string[] TankJobs = ["PLD", "WAR", "DRK", "GNB"];
    public static readonly string[] HealerJobs = ["WHM", "SCH", "AST", "SGE"];
    public static readonly string[] MeleeDPSJobs = ["MNK", "DRG", "NIN", "SAM", "RPR", "VPR"];
    public static readonly string[] RangedDPSJobs = ["BRD", "MCH", "DNC"];
    public static readonly string[] MagicDPSJobs = ["BLM", "SMN", "RDM", "PCT"];

    public static readonly string[] CombatJobs =
        [.. TankJobs, .. HealerJobs, .. MeleeDPSJobs, .. RangedDPSJobs, .. MagicDPSJobs];

    public static readonly string[] AllJobs = [.. DoHJobs, .. DoLJobs, .. CombatJobs];

    private static readonly HashSet<string> DoHSet = new(DoHJobs, StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> DoLSet = new(DoLJobs, StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> CombatSet = new(CombatJobs, StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> TankSet = new(TankJobs, StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> HealerSet = new(HealerJobs, StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> MeleeDPSSet = new(MeleeDPSJobs, StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> RangedDPSSet = new(RangedDPSJobs, StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> MagicDPSSet = new(MagicDPSJobs, StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> NoOffhandJobs =
        new(["CUL", "FSH"], StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, string> DisplayNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // DoH
            ["CRP"] = "Carpenter", ["BSM"] = "Blacksmith", ["ARM"] = "Armorer",
            ["GSM"] = "Goldsmith", ["LTW"] = "Leatherworker", ["WVR"] = "Weaver",
            ["ALC"] = "Alchemist", ["CUL"] = "Culinarian",
            // DoL
            ["MIN"] = "Miner", ["BTN"] = "Botanist", ["FSH"] = "Fisher",
            // Tank
            ["PLD"] = "Paladin", ["WAR"] = "Warrior", ["DRK"] = "Dark Knight", ["GNB"] = "Gunbreaker",
            // Healer
            ["WHM"] = "White Mage", ["SCH"] = "Scholar", ["AST"] = "Astrologian", ["SGE"] = "Sage",
            // Melee DPS
            ["MNK"] = "Monk", ["DRG"] = "Dragoon", ["NIN"] = "Ninja",
            ["SAM"] = "Samurai", ["RPR"] = "Reaper", ["VPR"] = "Viper",
            // Ranged DPS
            ["BRD"] = "Bard", ["MCH"] = "Machinist", ["DNC"] = "Dancer",
            // Magic DPS
            ["BLM"] = "Black Mage", ["SMN"] = "Summoner", ["RDM"] = "Red Mage", ["PCT"] = "Pictomancer"
        };

    private static readonly Dictionary<string, int> ClassJobIds =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // DoH/DoL
            ["CRP"] = 8, ["BSM"] = 9, ["ARM"] = 10, ["GSM"] = 11,
            ["LTW"] = 12, ["WVR"] = 13, ["ALC"] = 14, ["CUL"] = 15,
            ["MIN"] = 16, ["BTN"] = 17, ["FSH"] = 18,
            // Tank
            ["PLD"] = 19, ["WAR"] = 21, ["DRK"] = 32, ["GNB"] = 37,
            // Healer
            ["WHM"] = 24, ["SCH"] = 28, ["AST"] = 33, ["SGE"] = 40,
            // Melee DPS
            ["MNK"] = 20, ["DRG"] = 22, ["NIN"] = 30,
            ["SAM"] = 34, ["RPR"] = 39, ["VPR"] = 41,
            // Ranged DPS
            ["BRD"] = 23, ["MCH"] = 31, ["DNC"] = 38,
            // Magic DPS
            ["BLM"] = 25, ["SMN"] = 27, ["RDM"] = 35, ["PCT"] = 42
        };

    public static bool IsDoH(string job) => DoHSet.Contains(job);
    public static bool IsDoL(string job) => DoLSet.Contains(job);
    public static bool IsCombat(string job) => CombatSet.Contains(job);
    public static bool IsTank(string job) => TankSet.Contains(job);
    public static bool IsHealer(string job) => HealerSet.Contains(job);
    public static bool IsMeleeDPS(string job) => MeleeDPSSet.Contains(job);
    public static bool IsRangedDPS(string job) => RangedDPSSet.Contains(job);
    public static bool IsMagicDPS(string job) => MagicDPSSet.Contains(job);
    public static bool IsKnownJob(string job) => DisplayNames.ContainsKey(job);
    public static bool HasOffhand(string job) => !NoOffhandJobs.Contains(job);

    public static CombatRole? GetCombatRole(string job)
    {
        if (TankSet.Contains(job)) return CombatRole.Tank;
        if (HealerSet.Contains(job)) return CombatRole.Healer;
        if (MeleeDPSSet.Contains(job)) return CombatRole.MeleeDPS;
        if (RangedDPSSet.Contains(job)) return CombatRole.RangedDPS;
        if (MagicDPSSet.Contains(job)) return CombatRole.MagicDPS;
        return null;
    }

    public static string GetJobDisplayName(string job) =>
        DisplayNames.GetValueOrDefault(job, job);

    public static int GetClassJobId(string job) =>
        ClassJobIds.GetValueOrDefault(job, 0);
}
