using FFXIV.Domain.Entities;
using FFXIV.Domain.Enums;
using FFXIV.Domain.Interfaces;
using FFXIV.Infrastructure.ExternalApis.Models;
using FFXIV.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FFXIV.Infrastructure.DataSync;

/// <summary>
/// One row per consumable item and crafting stat.
/// </summary>
public sealed class ConsumableSyncService(
    AppDbContext db,
    IXivApiClient xivApiClient,
    ILogger<ConsumableSyncService> logger)
{
    private const int MedicineCategory = 44;
    private const int MealsCategory = 46;
    private const int WellFedStatus = 48;
    private const int MedicatedStatus = 49;
    private const int SearchPageSize = 100;
    private const int ItemFoodPageSize = 500;
    private const string ItemActionFields = "ItemAction.Data,ItemAction.DataHQ";
    private const string ItemFoodFields = "BaseParam@as(raw),IsRelative,Value,Max,ValueHQ,MaxHQ";

    // A page lost mid-paging passes the zero-row checks; the shrink guard refuses a snapshot that lost most of the table.
    private const int ShrinkGuardMinimumRows = 20;
    private const int ShrinkGuardRetainedPercent = 80;

    // Far above the few pages the real sheets take; past it, a pass is looping rather than paging.
    private const int MaxPages = 1_000;

    private static readonly Dictionary<int, ConsumableStat> CraftingBaseParams = new()
    {
        [70] = ConsumableStat.Craftsmanship,
        [71] = ConsumableStat.Control,
        [11] = ConsumableStat.CP,
    };

    public async Task<int> SyncConsumablesAsync(CancellationToken ct)
    {
        logger.LogInformation("Starting consumable sync...");

        // XivApiClient answers a null or empty body with an empty page (an HTTP error throws), so such
        // a response reads as an empty catalogue; failing here keeps one from being stored or exported.
        var meals = await SearchCategoryAsync(MealsCategory, "the meals search", ct);
        EnsureRows(meals.Count, "the meals search");
        var medicine = await SearchCategoryAsync(MedicineCategory, "the medicine search", ct);
        EnsureRows(medicine.Count, "the medicine search");
        var itemFood = await LoadItemFoodAsync(ct);
        EnsureRows(itemFood.Count, "the ItemFood sheet");

        var snapshot = BuildSnapshot(meals.Concat(medicine).DistinctBy(r => r.RowId), itemFood);
        var produced = await DropItemsMissingFromItemsAsync(snapshot.Rows, ct);
        // The shrink guard skips an empty table, so a pass with no usable item would store nothing and still mark the version synced.
        if (produced.Count == 0)
            throw new InvalidOperationException("consumable sync: the snapshot yields no rows; nothing changed");

        var existing = await db.ConsumableBonuses.ToListAsync(ct);
        if (existing.Count >= ShrinkGuardMinimumRows
            && produced.Count * 100 < existing.Count * ShrinkGuardRetainedPercent)
        {
            throw new InvalidOperationException(
                $"consumable sync: the snapshot yields {produced.Count} rows against {existing.Count} stored " +
                $"(below {ShrinkGuardRetainedPercent}%); nothing changed");
        }

        var existingByKey = existing.ToDictionary(r => (r.ItemId, r.Stat));
        var producedKeys = produced.Select(r => (r.ItemId, r.Stat)).ToHashSet();
        var added = 0;
        var updated = 0;

        foreach (var row in produced)
        {
            if (!existingByKey.TryGetValue((row.ItemId, row.Stat), out var tracked))
            {
                db.ConsumableBonuses.Add(row);
                added++;
            }
            else if (CopyIfChanged(row, tracked))
            {
                updated++;
            }
        }

        // An item missing from the snapshot or malformed in it keeps its rows; only an item read cleanly with no bonus loses them.
        var removed = existing
            .Where(r => snapshot.WellFormedItemIds.Contains(r.ItemId) && !producedKeys.Contains((r.ItemId, r.Stat)))
            .ToList();
        db.ConsumableBonuses.RemoveRange(removed);

        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();

        logger.LogInformation(
            "Consumable sync complete. {RowCount} rows for {ItemCount} items: {Added} added, {Updated} updated, {Removed} removed; {Malformed} malformed items kept their rows",
            produced.Count, produced.Select(r => r.ItemId).Distinct().Count(), added, updated, removed.Count, snapshot.MalformedCount);
        return produced.Count;
    }

    private static void EnsureRows(int count, string pass)
    {
        if (count == 0)
            throw new InvalidOperationException($"consumable sync: {pass} returned no rows; nothing changed");
    }

    private async Task<List<XivApiRow<XivApiConsumableItemFields>>> SearchCategoryAsync(
        int category, string pass, CancellationToken ct)
    {
        var rows = new List<XivApiRow<XivApiConsumableItemFields>>();
        var seenCursors = new HashSet<string>();
        string? cursor = null;
        var pages = 0;

        do
        {
            ct.ThrowIfCancellationRequested();
            EnsurePageBudget(++pages, pass);

            var (page, next) = await xivApiClient.SearchAsync<XivApiConsumableItemFields>(
                "Item", $"ItemUICategory={category}", ItemActionFields,
                limit: SearchPageSize, cursor: cursor, cancellationToken: ct);

            rows.AddRange(page);
            cursor = page.Count == 0 ? null : next;
            if (cursor is not null && !seenCursors.Add(cursor))
                throw PagingStalled(pass, $"returned cursor {cursor} twice");
        }
        while (cursor is not null);

        return rows;
    }

    private async Task<Dictionary<int, XivApiItemFoodFields>> LoadItemFoodAsync(CancellationToken ct)
    {
        const string pass = "the ItemFood sheet";
        var rows = new Dictionary<int, XivApiItemFoodFields>();
        int? afterRowId = null;
        var pages = 0;

        while (true)
        {
            ct.ThrowIfCancellationRequested();
            EnsurePageBudget(++pages, pass);

            var (page, lastRowId) = await xivApiClient.GetSheetAsync<XivApiItemFoodFields>(
                "ItemFood", ItemFoodFields,
                limit: ItemFoodPageSize, afterRowId: afterRowId, cancellationToken: ct);

            if (page.Count == 0) break;

            if (lastRowId is not { } last || last <= afterRowId)
                throw PagingStalled(pass, $"did not page past row {afterRowId ?? 0}");

            foreach (var row in page)
                rows[row.RowId] = row.Fields;

            afterRowId = last;
        }

        return rows;
    }

    private static void EnsurePageBudget(int pages, string pass)
    {
        if (pages > MaxPages)
            throw PagingStalled(pass, $"ran past {MaxPages} pages");
    }

    private static InvalidOperationException PagingStalled(string pass, string detail) =>
        new($"consumable sync: {pass} {detail}; nothing changed");

    private Snapshot BuildSnapshot(
        IEnumerable<XivApiRow<XivApiConsumableItemFields>> items,
        IReadOnlyDictionary<int, XivApiItemFoodFields> itemFood)
    {
        var rows = new List<ConsumableBonus>();
        var wellFormed = new HashSet<int>();
        var malformed = 0;
        var otherActions = 0;

        foreach (var item in items)
        {
            var action = item.Fields?.ItemAction?.Fields;
            if (action?.Data is not { Count: >= 2 } data)
            {
                logger.LogWarning("Consumable sync: item {ItemId} has no usable ItemAction; its rows are kept", item.RowId);
                malformed++;
                continue;
            }

            ConsumableKind? kind = data[0] switch
            {
                WellFedStatus => ConsumableKind.Meal,
                MedicatedStatus => ConsumableKind.Medicine,
                _ => null,
            };
            if (kind is null)
            {
                otherActions++;
                continue;
            }

            var foodRowId = data[1];
            if (action.DataHQ is { Count: >= 2 } hqData && hqData[1] != foodRowId)
            {
                logger.LogWarning(
                    "Consumable sync: item {ItemId} names ItemFood row {HqRowId} for HQ and {RowId} for NQ; the NQ row is used",
                    item.RowId, hqData[1], foodRowId);
            }

            var bonuses = itemFood.TryGetValue(foodRowId, out var food) ? ReadBonuses(item.RowId, kind.Value, food) : null;
            if (bonuses is null)
            {
                logger.LogWarning(
                    "Consumable sync: item {ItemId} names ItemFood row {RowId}, which is missing or malformed; its rows are kept",
                    item.RowId, foodRowId);
                malformed++;
                continue;
            }

            wellFormed.Add(item.RowId);
            rows.AddRange(bonuses);
        }

        if (otherActions > 0)
        {
            logger.LogInformation(
                "Consumable sync: {Count} searched items apply neither Well Fed nor Medicated and were skipped", otherActions);
        }

        return new Snapshot(rows, wellFormed, malformed);
    }

    // Null when the row cannot be read slot by slot; an empty list for a well-formed row with no
    // crafting bonus (combat food).
    private static List<ConsumableBonus>? ReadBonuses(int itemId, ConsumableKind kind, XivApiItemFoodFields? food)
    {
        if (food is not
            {
                BaseParam: { } baseParams,
                IsRelative: { } isRelative,
                Value: { } value,
                Max: { } max,
                ValueHQ: { } valueHq,
                MaxHQ: { } maxHq,
            })
        {
            return null;
        }

        var slots = baseParams.Count;
        if (isRelative.Count < slots || value.Count < slots || max.Count < slots
            || valueHq.Count < slots || maxHq.Count < slots)
        {
            return null;
        }

        var bonuses = new List<ConsumableBonus>();
        for (var i = 0; i < slots; i++)
        {
            if (!CraftingBaseParams.TryGetValue(baseParams[i], out var stat) || (value[i] == 0 && valueHq[i] == 0))
                continue;

            if (bonuses.Any(b => b.Stat == stat))
                return null;

            bonuses.Add(new ConsumableBonus
            {
                ItemId = itemId,
                Kind = kind,
                Stat = stat,
                IsRelative = isRelative[i],
                Value = value[i],
                Max = max[i],
                ValueHQ = valueHq[i],
                MaxHQ = maxHq[i],
            });
        }

        return bonuses;
    }

    private async Task<List<ConsumableBonus>> DropItemsMissingFromItemsAsync(List<ConsumableBonus> rows, CancellationToken ct)
    {
        var ids = rows.Select(r => r.ItemId).Distinct().ToList();
        var known = (await db.Items
                .AsNoTracking()
                .Where(i => ids.Contains(i.ItemId))
                .Select(i => i.ItemId)
                .ToListAsync(ct))
            .ToHashSet();

        var missing = ids.Where(id => !known.Contains(id)).ToList();
        if (missing.Count > 0)
        {
            logger.LogWarning(
                "Consumable sync: {Count} consumables are not in Items and were skipped: {ItemIds}",
                missing.Count, string.Join(", ", missing));
        }

        return rows.Where(r => known.Contains(r.ItemId)).ToList();
    }

    private static bool CopyIfChanged(ConsumableBonus source, ConsumableBonus target)
    {
        if (target.Kind == source.Kind
            && target.IsRelative == source.IsRelative
            && target.Value == source.Value
            && target.Max == source.Max
            && target.ValueHQ == source.ValueHQ
            && target.MaxHQ == source.MaxHQ)
        {
            return false;
        }

        target.Kind = source.Kind;
        target.IsRelative = source.IsRelative;
        target.Value = source.Value;
        target.Max = source.Max;
        target.ValueHQ = source.ValueHQ;
        target.MaxHQ = source.MaxHQ;
        return true;
    }

    private sealed record Snapshot(List<ConsumableBonus> Rows, HashSet<int> WellFormedItemIds, int MalformedCount);
}
