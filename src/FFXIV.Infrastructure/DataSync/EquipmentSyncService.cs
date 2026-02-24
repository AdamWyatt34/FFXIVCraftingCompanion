using FFXIV.Domain.Entities;
using FFXIV.Domain.Enums;
using FFXIV.Domain.Interfaces;
using FFXIV.Infrastructure.ExternalApis;
using FFXIV.Infrastructure.ExternalApis.Models;
using FFXIV.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FFXIV.Infrastructure.DataSync;

/// <summary>
/// Syncs equipment data from XIVAPI v2 Item sheet for all job categories,
/// filtering by EquipSlotCategory and ClassJobCategory reference sheets.
/// </summary>
public sealed class EquipmentSyncService
{
    private readonly AppDbContext _db;
    private readonly IXivApiClient _xivApiClient;
    private readonly DataSyncOptions _options;
    private readonly ILogger<EquipmentSyncService> _logger;

    private const int PageSize = 500;

    private const string EquipSlotCategoryFields = "MainHand,OffHand,Head,Body,Gloves,Legs,Feet,Ears,Neck,Wrists,FingerL,FingerR";
    private const string ClassJobCategoryFieldString = "Name,CRP,BSM,ARM,GSM,LTW,WVR,ALC,CUL,MIN,BTN,FSH,PLD,WAR,DRK,GNB,WHM,SCH,AST,SGE,MNK,DRG,NIN,SAM,RPR,VPR,BRD,MCH,DNC,BLM,SMN,RDM,PCT";
    private const string EquipmentItemFields = "Name,Icon,CanBeHq,LevelItem@as(raw),Rarity,EquipSlotCategory@as(raw),ClassJobCategory@as(raw),LevelEquip,BaseParam@as(raw),BaseParamValue,MateriaSlotCount";

    public EquipmentSyncService(
        AppDbContext db,
        IXivApiClient xivApiClient,
        IOptions<DataSyncOptions> options,
        ILogger<EquipmentSyncService> logger)
    {
        _db = db;
        _xivApiClient = xivApiClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<int> SyncEquipmentAsync(CancellationToken ct)
    {
        _logger.LogInformation("Starting equipment sync...");

        // Phase 1: Pre-load reference sheets
        var equipSlotLookup = await LoadEquipSlotCategoriesAsync(ct);
        _logger.LogInformation("Loaded {Count} equip slot categories", equipSlotLookup.Count);

        var (classJobStrings, validCategoryIds) = await LoadClassJobCategoriesAsync(ct);
        _logger.LogInformation("Loaded {Count} class job categories ({ValidCount} with jobs)",
            classJobStrings.Count, validCategoryIds.Count);

        // Phase 2: Scan Item sheet with filtering
        var equipmentIds = new HashSet<int>();
        var totalSynced = await ScanAndUpsertEquipmentAsync(
            equipSlotLookup, classJobStrings, validCategoryIds, equipmentIds, ct);

        // Phase 3: Link Recipe and Source data
        await LinkRecipeAndSourceDataAsync(equipmentIds, ct);

        _logger.LogInformation("Equipment sync complete. Total: {Count}", totalSynced);
        return totalSynced;
    }

    private async Task<Dictionary<int, EquipSlot>> LoadEquipSlotCategoriesAsync(CancellationToken ct)
    {
        var result = new Dictionary<int, EquipSlot>();
        int? afterRowId = null;

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var (rows, lastRowId) = await _xivApiClient.GetSheetAsync<XivApiEquipSlotCategoryFields>(
                "EquipSlotCategory", EquipSlotCategoryFields,
                limit: PageSize, afterRowId: afterRowId, cancellationToken: ct);

            if (rows.Count == 0) break;

            foreach (var row in rows)
            {
                var slot = XivApiDataMapper.ResolveEquipSlot(row.Fields);
                if (slot.HasValue)
                {
                    result[row.RowId] = slot.Value;
                }
            }

            afterRowId = lastRowId;
        }

        return result;
    }

    private async Task<(Dictionary<int, string> Strings, HashSet<int> ValidIds)> LoadClassJobCategoriesAsync(CancellationToken ct)
    {
        var strings = new Dictionary<int, string>();
        var validIds = new HashSet<int>();
        int? afterRowId = null;

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var (rows, lastRowId) = await _xivApiClient.GetSheetAsync<XivApiClassJobCategoryFields>(
                "ClassJobCategory", ClassJobCategoryFieldString,
                limit: PageSize, afterRowId: afterRowId, cancellationToken: ct);

            if (rows.Count == 0) break;

            foreach (var row in rows)
            {
                var jobString = XivApiDataMapper.BuildClassJobString(row.Fields);
                if (!string.IsNullOrEmpty(jobString))
                {
                    strings[row.RowId] = jobString;
                    validIds.Add(row.RowId);
                }
            }

            afterRowId = lastRowId;
        }

        return (strings, validIds);
    }

    private async Task<int> ScanAndUpsertEquipmentAsync(
        Dictionary<int, EquipSlot> equipSlotLookup,
        Dictionary<int, string> classJobStrings,
        HashSet<int> validCategoryIds,
        HashSet<int> equipmentIds,
        CancellationToken ct)
    {
        int? afterRowId = null;
        var totalSynced = 0;
        var batch = new List<Equipment>();

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var (rows, lastRowId) = await _xivApiClient.GetSheetAsync<XivApiEquipmentItemFields>(
                "Item", EquipmentItemFields,
                limit: PageSize, afterRowId: afterRowId, cancellationToken: ct);

            if (rows.Count == 0) break;

            foreach (var row in rows)
            {
                if (string.IsNullOrWhiteSpace(row.Fields.Name))
                    continue;

                if (row.Fields.EquipSlotCategoryRowId == 0)
                    continue;

                if (!validCategoryIds.Contains(row.Fields.ClassJobCategoryRowId))
                    continue;

                if (!equipSlotLookup.TryGetValue(row.Fields.EquipSlotCategoryRowId, out var equipSlot))
                    continue;

                var classJobString = classJobStrings.GetValueOrDefault(row.Fields.ClassJobCategoryRowId, string.Empty);
                var stats = XivApiDataMapper.ExtractEquipmentStats(row.Fields.BaseParam, row.Fields.BaseParamValue);

                batch.Add(new Equipment
                {
                    EquipmentId = row.RowId,
                    Name = row.Fields.Name,
                    EquipSlot = equipSlot,
                    ClassJobCategory = classJobString,
                    EquipLevel = row.Fields.LevelEquip,
                    ItemLevel = row.Fields.LevelItem,
                    // DoH/DoL stats
                    Craftsmanship = stats.Craftsmanship,
                    Control = stats.Control,
                    CP = stats.CP,
                    Gathering = stats.Gathering,
                    Perception = stats.Perception,
                    GP = stats.GP,
                    // Combat stats
                    Vitality = stats.Vitality,
                    Strength = stats.Strength,
                    Dexterity = stats.Dexterity,
                    Intelligence = stats.Intelligence,
                    Mind = stats.Mind,
                    Piety = stats.Piety,
                    Tenacity = stats.Tenacity,
                    DirectHitRate = stats.DirectHitRate,
                    CriticalHit = stats.CriticalHit,
                    Determination = stats.Determination,
                    SkillSpeed = stats.SkillSpeed,
                    SpellSpeed = stats.SpellSpeed,
                    PhysicalDamage = stats.PhysicalDamage,
                    Defense = stats.Defense,
                    MagicDefense = stats.MagicDefense,
                    MateriaSlots = row.Fields.MateriaSlotCount,
                    IconPath = XivApiDataMapper.BuildIconUrl(row.Fields.Icon),
                    CanBeHQ = row.Fields.CanBeHq,
                    Rarity = row.Fields.Rarity
                });

                equipmentIds.Add(row.RowId);

                if (batch.Count >= _options.BatchSize)
                {
                    await UpsertEquipmentBatchAsync(batch, ct);
                    totalSynced += batch.Count;
                    batch.Clear();

                    if (totalSynced % 1000 < _options.BatchSize)
                        _logger.LogInformation("Synced {Count} equipment so far...", totalSynced);
                }
            }

            afterRowId = lastRowId;
        }

        if (batch.Count > 0)
        {
            await UpsertEquipmentBatchAsync(batch, ct);
            totalSynced += batch.Count;
            batch.Clear();
        }

        return totalSynced;
    }

    private async Task LinkRecipeAndSourceDataAsync(HashSet<int> equipmentIds, CancellationToken ct)
    {
        if (equipmentIds.Count == 0) return;

        _logger.LogInformation("Linking recipe and source data for {Count} equipment items...", equipmentIds.Count);

        // Build RecipeId lookup: ResultItemId → RecipeId
        var recipeLookup = await _db.Recipes
            .AsNoTracking()
            .Where(r => equipmentIds.Contains(r.ResultItemId))
            .ToDictionaryAsync(r => r.ResultItemId, r => r.RecipeId, ct);

        // Build Source lookup: ItemId → (SourceType, SourceDetails)
        var sourceLookup = await _db.ItemSources
            .AsNoTracking()
            .Where(s => equipmentIds.Contains(s.ItemId))
            .GroupBy(s => s.ItemId)
            .ToDictionaryAsync(
                g => g.Key,
                g => (g.First().SourceType, g.First().SourceDetails),
                ct);

        _logger.LogInformation("Found {RecipeCount} recipes and {SourceCount} sources for equipment",
            recipeLookup.Count, sourceLookup.Count);

        // Batch update tracked entities
        var idList = equipmentIds.ToList();
        foreach (var chunk in idList.Chunk(_options.BatchSize))
        {
            var chunkSet = chunk.ToHashSet();
            var tracked = await _db.Equipment
                .Where(e => chunkSet.Contains(e.EquipmentId))
                .ToListAsync(ct);

            foreach (var equip in tracked)
            {
                if (recipeLookup.TryGetValue(equip.EquipmentId, out var recipeId))
                    equip.RecipeId = recipeId;

                if (sourceLookup.TryGetValue(equip.EquipmentId, out var source))
                {
                    equip.SourceType = source.SourceType;
                    equip.SourceDetails = source.SourceDetails;
                }
            }

            await _db.SaveChangesAsync(ct);
            _db.ChangeTracker.Clear();
        }
    }

    private async Task UpsertEquipmentBatchAsync(List<Equipment> equipment, CancellationToken ct)
    {
        var ids = equipment.Select(e => e.EquipmentId).ToHashSet();
        var existing = await _db.Equipment
            .Where(e => ids.Contains(e.EquipmentId))
            .ToDictionaryAsync(e => e.EquipmentId, ct);

        foreach (var equip in equipment)
        {
            if (existing.TryGetValue(equip.EquipmentId, out var tracked))
            {
                tracked.Name = equip.Name;
                tracked.EquipSlot = equip.EquipSlot;
                tracked.ClassJobCategory = equip.ClassJobCategory;
                tracked.EquipLevel = equip.EquipLevel;
                tracked.ItemLevel = equip.ItemLevel;
                // DoH/DoL stats
                tracked.Craftsmanship = equip.Craftsmanship;
                tracked.Control = equip.Control;
                tracked.CP = equip.CP;
                tracked.Gathering = equip.Gathering;
                tracked.Perception = equip.Perception;
                tracked.GP = equip.GP;
                // Combat stats
                tracked.Vitality = equip.Vitality;
                tracked.Strength = equip.Strength;
                tracked.Dexterity = equip.Dexterity;
                tracked.Intelligence = equip.Intelligence;
                tracked.Mind = equip.Mind;
                tracked.Piety = equip.Piety;
                tracked.Tenacity = equip.Tenacity;
                tracked.DirectHitRate = equip.DirectHitRate;
                tracked.CriticalHit = equip.CriticalHit;
                tracked.Determination = equip.Determination;
                tracked.SkillSpeed = equip.SkillSpeed;
                tracked.SpellSpeed = equip.SpellSpeed;
                tracked.PhysicalDamage = equip.PhysicalDamage;
                tracked.Defense = equip.Defense;
                tracked.MagicDefense = equip.MagicDefense;
                tracked.MateriaSlots = equip.MateriaSlots;
                tracked.IconPath = equip.IconPath;
                tracked.CanBeHQ = equip.CanBeHQ;
                tracked.Rarity = equip.Rarity;
            }
            else
            {
                _db.Equipment.Add(equip);
            }
        }

        await _db.SaveChangesAsync(ct);
        _db.ChangeTracker.Clear();
    }
}
