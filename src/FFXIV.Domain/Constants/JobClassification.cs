namespace FFXIV.Domain.Constants;

public static class JobClassification
{
    public static readonly string[] DoHJobs = ["CRP", "BSM", "ARM", "GSM", "LTW", "WVR", "ALC", "CUL"];
    public static readonly string[] DoLJobs = ["MIN", "BTN", "FSH"];

    private static readonly HashSet<string> DoHSet = new(DoHJobs, StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> DoLSet = new(DoLJobs, StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> NoOffhandJobs =
        new(["CUL", "FSH"], StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, string> DisplayNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["CRP"] = "Carpenter",
            ["BSM"] = "Blacksmith",
            ["ARM"] = "Armorer",
            ["GSM"] = "Goldsmith",
            ["LTW"] = "Leatherworker",
            ["WVR"] = "Weaver",
            ["ALC"] = "Alchemist",
            ["CUL"] = "Culinarian",
            ["MIN"] = "Miner",
            ["BTN"] = "Botanist",
            ["FSH"] = "Fisher"
        };

    private static readonly Dictionary<string, int> ClassJobIds =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["CRP"] = 8, ["BSM"] = 9, ["ARM"] = 10, ["GSM"] = 11,
            ["LTW"] = 12, ["WVR"] = 13, ["ALC"] = 14, ["CUL"] = 15,
            ["MIN"] = 16, ["BTN"] = 17, ["FSH"] = 18
        };

    public static bool IsDoH(string job) => DoHSet.Contains(job);
    public static bool IsDoL(string job) => DoLSet.Contains(job);
    public static bool HasOffhand(string job) => !NoOffhandJobs.Contains(job);

    public static string GetJobDisplayName(string job) =>
        DisplayNames.GetValueOrDefault(job, job);

    public static int GetClassJobId(string job) =>
        ClassJobIds.GetValueOrDefault(job, 0);
}
