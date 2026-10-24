namespace FFXIV.Application.Rotations;

public sealed record RotationActionEntry(int Id, string Name, bool IsLegacy);

public static class RotationActionCatalog
{
    private static readonly RotationActionEntry[] Entries =
    [
        new(100001, "BasicSynthesis", false),
        new(100002, "BasicTouch", false),
        new(100003, "MastersMend", false),
        new(100004, "StandardTouch", false),
        new(100010, "Observe", false),
        new(100033, "RapidSynthesis", false),
        new(100203, "CarefulSynthesis", false),
        new(100209, "Innovation", false),
        new(100227, "PrudentTouch", false),
        new(100235, "FocusedSynthesis", true),
        new(100243, "FocusedTouch", true),
        new(100252, "InnerQuiet", true),
        new(100260, "GreatStrides", false),
        new(100278, "Manipulation", false),
        new(100279, "WasteNot", false),
        new(100283, "TrainedEye", false),
        new(100285, "WasteNotII", false),
        new(100297, "Veneration", false),
        new(100299, "PreparatoryTouch", false),
        new(100315, "IntensiveSynthesis", false),
        new(100317, "HeartAndSoul", false),
        new(100323, "DelicateSynthesis", false),
        new(100339, "ByregotsBlessing", false),
        new(100355, "HastyTouch", false),
        new(100364, "FinalAppraisal", false),
        new(100371, "TricksOfTheTrade", false),
        new(100379, "MuscleMemory", false),
        new(100387, "Reflect", false),
        new(100395, "CarefulObservation", false),
        new(100403, "Groundwork", false),
        new(100411, "AdvancedTouch", false),
        new(100427, "PrudentSynthesis", false),
        new(100435, "TrainedFinesse", false),
        new(100439, "ImmaculateMend", false),
        new(100443, "QuickInnovation", false),
        new(100451, "TrainedPerfection", false),
        new(100501, "PreciseTouch", false),
        new(100502, "RefinedTouch", false),
        new(100503, "DaringTouch", false),
    ];

    public static RotationActionEntry? GetById(int id) => Entries.FirstOrDefault(action => action.Id == id);

    public static IEnumerable<RotationActionEntry> All(bool includeLegacy = false) =>
        Entries.Where(action => includeLegacy || !action.IsLegacy);
}
