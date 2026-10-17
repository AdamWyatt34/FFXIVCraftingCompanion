namespace FFXIV.Domain.Constants;

/// <summary>
/// The slot keys a profile or saved gear set may carry. Requests are checked against this list
/// before anything is stored, so the JSON columns never pick up arbitrary keys.
/// </summary>
public static class GearSlotKeys
{
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        "Head", "Body", "Hands", "Legs", "Feet",
        "MainHand", "OffHand",
        "Earring", "Necklace", "Bracelet",
        "Ring1", "Ring2",
    };

    public static bool IsValid(string slotKey) => All.Contains(slotKey);

    public static string Describe() => string.Join(", ", All);
}
