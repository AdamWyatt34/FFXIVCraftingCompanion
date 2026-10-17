using FFXIV.Domain.Entities;
using FFXIV.Domain.Enums;

namespace FFXIV.Export;

// GetBestForSlotsAsync returns, per slot, the top N by item level among pieces equippable at
// the requested level. A piece can only ever be returned if it ranks in the top N at its own
// equip level, since raising the level only adds competitors. Keeping exactly those pieces,
// in the query's order, lets the frontend answer every level without the full table.
public static class GearFrontier
{
    public const int CandidateLimit = 50;

    public static IReadOnlyDictionary<string, IReadOnlyList<Equipment>> Build(IEnumerable<Equipment> equipment, int limit = CandidateLimit)
    {
        var result = new Dictionary<string, IReadOnlyList<Equipment>>();
        foreach (var slot in equipment.GroupBy(e => e.EquipSlot).OrderBy(g => g.Key))
        {
            var ordered = slot.OrderByDescending(e => e.ItemLevel).ThenBy(e => e.EquipmentId).ToList();
            var kept = new List<Equipment>();
            for (var i = 0; i < ordered.Count; i++)
            {
                var piece = ordered[i];
                var ahead = 0;
                for (var j = 0; j < i && ahead < limit; j++)
                    if (ordered[j].EquipLevel <= piece.EquipLevel)
                        ahead++;
                if (ahead < limit)
                    kept.Add(piece);
            }
            result[slot.Key.ToString()] = kept;
        }
        return result;
    }
}
