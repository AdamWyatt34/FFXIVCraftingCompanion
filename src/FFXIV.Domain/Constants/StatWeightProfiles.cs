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

    public static readonly IReadOnlyList<StatWeightProfile> DoHProfiles =
        [Balanced, ProgressFocused, QualityFocused, CpFocused];

    public static readonly IReadOnlyList<StatWeightProfile> DoLProfiles =
        [GatheringBalanced, PerceptionFocused];

    private static readonly Dictionary<string, StatWeightProfile> AllProfiles =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Balanced"] = Balanced,
            ["ProgressFocused"] = ProgressFocused,
            ["QualityFocused"] = QualityFocused,
            ["CpFocused"] = CpFocused,
            ["GatheringBalanced"] = GatheringBalanced,
            ["PerceptionFocused"] = PerceptionFocused
        };

    public static StatWeightProfile GetProfile(string name) =>
        AllProfiles.GetValueOrDefault(name, Balanced);
}
