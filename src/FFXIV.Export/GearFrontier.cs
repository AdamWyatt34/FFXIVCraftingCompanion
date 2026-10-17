using FFXIV.Domain.Entities;
using FFXIV.Domain.Enums;

namespace FFXIV.Export;

// GetBestForSlotsAsync returns, per slot, the top N by item level among pieces equippable at
// the requested level, drawn from every piece or from crafted pieces only. A piece can only
// ever be returned if it ranks within N at its own equip level in the pool it belongs to, since
// raising the level only adds competitors. Keeping exactly those pieces, in the query's order,
// lets the frontend answer every level and both modes without the full table.
public static class GearFrontier
{
    public const int CandidateLimit = 10;

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
                var crafted = piece.SourceType == ItemSourceType.Crafted;
                var ahead = 0;
                var craftedAhead = 0;
                for (var j = 0; j < i; j++)
                {
                    if (ordered[j].EquipLevel > piece.EquipLevel) continue;
                    ahead++;
                    if (ordered[j].SourceType == ItemSourceType.Crafted) craftedAhead++;
                }
                if (ahead < limit || (crafted && craftedAhead < limit))
                    kept.Add(piece);
            }
            result[slot.Key.ToString()] = kept;
        }
        return result;
    }
}
