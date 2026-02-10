namespace FFXIV.Domain.Constants;

public static class JobNameMapping
{
    private static readonly Dictionary<string, string> NameToAbbreviation =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // DoH
            ["Carpenter"] = "CRP",
            ["Blacksmith"] = "BSM",
            ["Armorer"] = "ARM",
            ["Goldsmith"] = "GSM",
            ["Leatherworker"] = "LTW",
            ["Weaver"] = "WVR",
            ["Alchemist"] = "ALC",
            ["Culinarian"] = "CUL",

            // DoL
            ["Miner"] = "MIN",
            ["Botanist"] = "BTN",
            ["Fisher"] = "FSH",
        };

    /// <summary>
    /// Converts a NetStone job name (e.g., "Carpenter / Carpenter") to our abbreviation ("CRP").
    /// Returns null if the job is not a DoH/DoL job.
    /// </summary>
    public static string? ToAbbreviation(string netStoneName)
    {
        // NetStone returns "Class / Job" format, e.g., "Carpenter / Carpenter"
        // For classes without a separate job name, it may just be "Carpenter"
        var name = netStoneName.Contains('/')
            ? netStoneName.Split('/').Last().Trim()
            : netStoneName.Trim();

        return NameToAbbreviation.GetValueOrDefault(name);
    }
}
