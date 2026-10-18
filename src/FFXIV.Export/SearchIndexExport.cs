using FFXIV.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FFXIV.Export;

/// <summary>
/// The item search index: one entry per exported item with the recipe and equipment facts the
/// frontend's search and quick filters read. Written twice, as search/items.json (the entries)
/// and as search/compact.json (<see cref="CompactSearchIndex"/>, the same content in a third of
/// the bytes); the frontend prefers the compact file and falls back to the entries for a data
/// set that predates it.
/// </summary>
public static class SearchIndexExport
{
    public static readonly string IndexPath = Path.Combine("search", "items.json");
    public static readonly string CompactPath = Path.Combine("search", "compact.json");

    public static async Task<List<SearchIndexEntry>> BuildEntriesAsync(AppDbContext db, IReadOnlySet<int> wanted, CancellationToken ct)
    {
        var equipment = await db.Equipment.AsNoTracking()
            .Select(e => new { e.EquipmentId, e.EquipSlot, e.EquipLevel, e.ClassJobCategory })
            .ToDictionaryAsync(e => e.EquipmentId, e => new SearchIndexEquipment(e.EquipSlot.ToString(), e.EquipLevel, e.ClassJobCategory), ct);
        var classJobLevels = await db.RecipeLevelEntries.AsNoTracking()
            .ToDictionaryAsync(l => l.RecipeLevel, l => l.ClassJobLevel, ct);
        var recipesByItem = (await db.Recipes.AsNoTracking()
                .Select(r => new { r.ResultItemId, r.RecipeId, r.CraftType, r.RecipeLevel })
                .OrderBy(r => r.RecipeId)
                .ToListAsync(ct))
            .GroupBy(r => r.ResultItemId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<SearchIndexRecipe>)g
                .Select(r => new SearchIndexRecipe(r.RecipeId, r.CraftType.ToString(), classJobLevels.GetValueOrDefault(r.RecipeLevel)))
                .ToList());

        var entries = new List<SearchIndexEntry>();
        await foreach (var item in db.Items.AsNoTracking().Where(i => i.Name != "").OrderBy(i => i.ItemId).AsAsyncEnumerable().WithCancellation(ct))
        {
            if (!wanted.Contains(item.ItemId))
                continue;
            var dto = Application.Mapping.Mapper.ToDto(item);
            entries.Add(new SearchIndexEntry(
                dto.ItemId, dto.Name, IconUrl.ToId(dto.IconUrl), dto.ItemLevel, dto.Category, dto.CanBeHQ,
                recipesByItem.GetValueOrDefault(item.ItemId) ?? [],
                equipment.GetValueOrDefault(item.ItemId)));
        }

        return entries;
    }

    /// <summary>Interns the repeated strings and lays the entries out as columns; lossless.</summary>
    public static CompactSearchIndex Compact(IReadOnlyList<SearchIndexEntry> entries)
    {
        var craftTypes = new Interner();
        var categories = new Interner();
        var slots = new Interner();
        var classJobCategories = new Interner();
        var count = entries.Count;
        var itemIds = new int[count];
        var names = new string[count];
        var icons = new int[count];
        var itemLevels = new int[count];
        var categoryIndexes = new int[count];
        var canBeHQ = new bool[count];
        var recipes = new IReadOnlyList<int[]>[count];
        var equipment = new int[]?[count];

        for (var i = 0; i < count; i++)
        {
            var entry = entries[i];
            itemIds[i] = entry.ItemId;
            names[i] = entry.Name;
            icons[i] = entry.Icon;
            itemLevels[i] = entry.ItemLevel;
            categoryIndexes[i] = categories.Index(entry.Category);
            canBeHQ[i] = entry.CanBeHQ;
            recipes[i] = entry.Recipes
                .Select(r => new[] { r.RecipeId, craftTypes.Index(r.CraftType), r.Level })
                .ToList();
            equipment[i] = entry.Equipment is null
                ? null
                : [slots.Index(entry.Equipment.Slot), entry.Equipment.EquipLevel, classJobCategories.Index(entry.Equipment.ClassJobCategory)];
        }

        return new CompactSearchIndex(
            craftTypes.Values, categories.Values, slots.Values, classJobCategories.Values,
            itemIds, names, icons, itemLevels, categoryIndexes, canBeHQ, recipes, equipment);
    }

    /// <summary>The entries a <see cref="CompactSearchIndex"/> encodes; the frontend's port of this is what runs in the browser.</summary>
    public static List<SearchIndexEntry> Inflate(CompactSearchIndex compact)
    {
        var entries = new List<SearchIndexEntry>(compact.ItemIds.Count);
        for (var i = 0; i < compact.ItemIds.Count; i++)
        {
            var gear = compact.Equipment[i];
            entries.Add(new SearchIndexEntry(
                compact.ItemIds[i], compact.Names[i], compact.Icons[i], compact.ItemLevels[i],
                compact.Categories[compact.CategoryIndexes[i]], compact.CanBeHQ[i],
                compact.Recipes[i].Select(r => new SearchIndexRecipe(r[0], compact.CraftTypes[r[1]], r[2])).ToList(),
                gear is null ? null : new SearchIndexEquipment(compact.Slots[gear[0]], gear[1], compact.ClassJobCategories[gear[2]])));
        }

        return entries;
    }

    private sealed class Interner
    {
        private readonly Dictionary<string, int> _indexes = new(StringComparer.Ordinal);
        private readonly List<string> _values = [];

        public IReadOnlyList<string> Values => _values;

        public int Index(string value)
        {
            if (_indexes.TryGetValue(value, out var index))
                return index;
            index = _values.Count;
            _values.Add(value);
            _indexes[value] = index;
            return index;
        }
    }
}
