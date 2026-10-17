using FFXIV.Domain.Entities;
using FFXIV.Domain.Enums;

namespace FFXIV.Export;

// GetBestForSlotsAsync returns, per slot, the top N by item level among pieces equippable at
// the requested level: 10 normally, 50 when the result is then filtered to crafted pieces. A
// piece can only ever be returned if it ranks within that limit at its own equip level, since
// raising the level only adds competitors. Keeping exactly those pieces, in the query's order,
// lets the frontend answer every level and both modes without the full table.
public static class GearFrontier
{
    public const int CandidateLimit = 10;
    public const int CraftedCandidateLimit = 50;

    public static IReadOnlyDictionary<string, IReadOnlyList<Equipment>> Build(
        IEnumerable<Equipment> equipment, int limit = CandidateLimit, int craftedLimit = CraftedCandidateLimit)
    {
        var result = new Dictionary<string, IReadOnlyList<Equipment>>();
        foreach (var slot in equipment.GroupBy(e => e.EquipSlot).OrderBy(g => g.Key))
        {
            var ordered = slot.OrderByDescending(e => e.ItemLevel).ThenBy(e => e.EquipmentId).ToList();
            var kept = new List<Equipment>();
            for (var i = 0; i < ordered.Count; i++)
            {
                var piece = ordered[i];
                var allowed = piece.SourceType == ItemSourceType.Crafted ? Math.Max(limit, craftedLimit) : limit;
                var ahead = 0;
                for (var j = 0; j < i && ahead < allowed; j++)
                    if (ordered[j].EquipLevel <= piece.EquipLevel)
                        ahead++;
                if (ahead < allowed)
                    kept.Add(piece);
            }
            result[slot.Key.ToString()] = kept;
        }
        return result;
    }
}
