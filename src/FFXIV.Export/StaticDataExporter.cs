using System.Text.Encodings.Web;
using System.Text.Json;
using FFXIV.Application.DTOs;
using FFXIV.Application.Gear;
using FFXIV.Application.Gear.DTOs;
using FFXIV.Application.Items.Queries;
using FFXIV.Application.Journey.Queries;
using FFXIV.Application.Recipes.Queries;
using FFXIV.Domain.Constants;
using FFXIV.Domain.Interfaces;
using FFXIV.Domain.Repositories;
using FFXIV.Infrastructure.DataSync;
using FFXIV.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FFXIV.Export;

public sealed class StaticDataExporter(
    ExportOptions options,
    AppDbContext db,
    DataSyncOrchestrator sync,
    IXivApiClient xivApi,
    IServiceScopeFactory scopeFactory,
    FixtureWriter fixtureWriter,
    ILogger<StaticDataExporter> logger)
{
    public const int SchemaVersion = 1;

    private readonly Dictionary<int, MaterialItemExport> _crystals = new();

    // Matches ASP.NET Core's controller serialization (web defaults) so the files carry the
    // same JSON the API would have sent.
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public async Task RunAsync(CancellationToken ct)
    {
        await db.Database.MigrateAsync(ct);

        if (!options.SkipSync)
        {
            logger.LogInformation("Running the XIVAPI sync");
            await sync.RunFullSyncAsync(ct);
        }

        if (options.FixtureOnly)
        {
            await fixtureWriter.WriteAsync(options.FixtureRecipeIds, options.FixtureJobs, options.FixtureOut!, ct);
            return;
        }

        var (versionKey, versionName) = await ResolveGameVersionAsync(ct);
        var exportedAt = DateTimeOffset.UtcNow;
        var version = $"{SanitizeForPath(versionName)}-{exportedAt:yyyyMMdd-HHmm}";
        logger.LogInformation("Exporting game version {Version} ({Key}) as {ExportVersion}", versionName, versionKey, version);

        Directory.CreateDirectory(options.OutDir);

        var recipeIds = await db.Recipes.AsNoTracking().Select(r => r.RecipeId).OrderBy(id => id).ToListAsync(ct);
        var itemIds = await db.Items.AsNoTracking().Where(i => i.Name != "").Select(i => i.ItemId).OrderBy(id => id).ToListAsync(ct);

        await ExportBucketsAsync("recipes", recipeIds, ExportRecipeBucketAsync, ct);
        await ExportBucketsAsync("items", itemIds, ExportItemBucketAsync, ct);
        Directory.CreateDirectory(Path.Combine(options.OutDir, "materials"));
        await WriteAsync(Path.Combine("materials", "crystals.json"), new SortedDictionary<int, MaterialItemExport>(_crystals), ct);
        var equipmentCount = await ExportGearAsync(ct);
        await ExportSearchIndexAsync(ct);

        var manifest = new Manifest(
            SchemaVersion, version, versionName, versionKey, exportedAt, options.BucketSize,
            new ManifestCounts(recipeIds.Count, itemIds.Count, equipmentCount, JobClassification.AllJobs.Length));
        await WriteAsync("manifest.json", manifest, ct);

        if (options.FixtureOut is not null)
            await fixtureWriter.WriteAsync(options.FixtureRecipeIds, options.FixtureJobs, options.FixtureOut, ct);

        logger.LogInformation("Export complete: {Recipes} recipes, {Items} items, {Equipment} equipment rows",
            recipeIds.Count, itemIds.Count, equipmentCount);
    }

    private async Task<(string Key, string Name)> ResolveGameVersionAsync(CancellationToken ct)
    {
        var synced = await db.SyncMetadata.AsNoTracking()
            .Where(s => s.EntityType == "GameVersion")
            .Select(s => s.LastPatchVersion)
            .FirstOrDefaultAsync(ct);
        if (synced is null)
            throw new InvalidOperationException("The database has never been synced; run without --skip-sync.");

        try
        {
            var (key, name) = await xivApi.GetCurrentGameVersionAsync(ct);
            if (key == synced)
                return (key, name);
            logger.LogWarning("Database holds game version {Synced} but XIVAPI reports {Current}; exporting what was synced", synced, key);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "XIVAPI unreachable; naming the export after the synced version key");
        }

        return (synced, synced);
    }

    public static int BucketOf(int id, int bucketSize) => id / bucketSize;

    private async Task ExportBucketsAsync(
        string folder,
        IReadOnlyList<int> ids,
        Func<IServiceProvider, IReadOnlyList<int>, CancellationToken, Task<string>> exportBucket,
        CancellationToken ct)
    {
        Directory.CreateDirectory(Path.Combine(options.OutDir, folder));
        var buckets = ids.GroupBy(id => BucketOf(id, options.BucketSize))
            .Select(g => (Bucket: g.Key, Ids: (IReadOnlyList<int>)g.ToList()))
            .ToList();

        var done = 0;
        await Parallel.ForEachAsync(buckets,
            new ParallelOptions { MaxDegreeOfParallelism = options.Parallelism, CancellationToken = ct },
            async (bucket, token) =>
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var json = await exportBucket(scope.ServiceProvider, bucket.Ids, token);
                await File.WriteAllTextAsync(Path.Combine(options.OutDir, folder, $"{bucket.Bucket}.json"), json, token);

                var count = Interlocked.Increment(ref done);
                if (count % 50 == 0 || count == buckets.Count)
                    logger.LogInformation("{Folder}: {Done}/{Total} buckets", folder, count, buckets.Count);
            });
    }

    private async Task<string> ExportRecipeBucketAsync(IServiceProvider services, IReadOnlyList<int> ids, CancellationToken ct)
    {
        var mediator = services.GetRequiredService<IMediator>();
        var bucket = new SortedDictionary<int, RecipeExport>();
        foreach (var id in ids)
        {
            var recipe = await mediator.Send(new GetRecipeByIdQuery(id, IncludeMaterials: true), ct)
                ?? throw new InvalidOperationException($"Recipe {id} vanished during export.");
            var parameters = await mediator.Send(new GetRecipeCraftingParametersQuery(id), ct)
                ?? throw new InvalidOperationException($"Recipe {id} has no crafting parameters.");
            var materials = await mediator.Send(new GetMaterialTreeQuery(id, Quantity: 1), ct)
                ?? throw new InvalidOperationException($"Recipe {id} has no material tree.");
            bucket[id] = new RecipeExport(recipe, parameters, MaterialGraph.Build(materials, _crystals));
        }
        return JsonSerializer.Serialize(bucket, Json);
    }

    private static async Task<string> ExportItemBucketAsync(IServiceProvider services, IReadOnlyList<int> ids, CancellationToken ct)
    {
        var mediator = services.GetRequiredService<IMediator>();
        var gatheringRepo = services.GetRequiredService<IGatheringInfoRepository>();
        var equipmentRepo = services.GetRequiredService<IEquipmentRepository>();
        var gathering = await gatheringRepo.GetByItemIdsAsync(ids, ct);
        var equipment = (await equipmentRepo.GetByIdsAsync(ids, ct)).ToDictionary(e => e.EquipmentId);

        var bucket = new SortedDictionary<int, ItemExport>();
        foreach (var id in ids)
        {
            var item = await mediator.Send(new GetItemByIdQuery(id), ct)
                ?? throw new InvalidOperationException($"Item {id} vanished during export.");
            var context = await mediator.Send(new GetItemContextQuery(id), ct)
                ?? throw new InvalidOperationException($"Item {id} has no context.");
            var nodes = gathering.TryGetValue(id, out var rows)
                ? rows.OrderBy(g => g.GatheringInfoId).Select(g => new GatheringNodeExport(
                    g.GatheringType, g.ClassJob, g.Level, g.Stars, g.ZoneName, g.AreaName,
                    g.CoordinateX, g.CoordinateY, g.IsTimed, g.SpawnTimes, g.IsHidden, g.NodeType)).ToList()
                : [];
            var gear = equipment.TryGetValue(id, out var piece) ? GearMapper.ToDto(piece, GearMapper.BuildSourceInfo(piece), 0) : null;
            bucket[id] = new ItemExport(item, context, nodes, gear);
        }
        return JsonSerializer.Serialize(bucket, Json);
    }

    private async Task<int> ExportGearAsync(CancellationToken ct)
    {
        Directory.CreateDirectory(Path.Combine(options.OutDir, "gear"));

        await using var scope = scopeFactory.CreateAsyncScope();
        var equipmentRepo = scope.ServiceProvider.GetRequiredService<IEquipmentRepository>();
        var gearService = scope.ServiceProvider.GetRequiredService<IGearRecommendationService>();

        var total = 0;
        foreach (var job in JobClassification.AllJobs)
        {
            var equipment = await equipmentRepo.GetByJobAndLevelAsync(job, int.MaxValue, ct);
            var frontier = GearFrontier.Build(equipment);
            var profiles = gearService.GetAvailableProfiles(job)
                .Select(p => new StatWeightProfileExport(p.Name, GearMapper.ToDto(p).DisplayName, p.Description, StatWeights(p)))
                .ToList();
            var export = new GearJobExport(
                job,
                JobClassification.GetJobDisplayName(job),
                profiles,
                frontier.ToDictionary(
                    kv => kv.Key,
                    kv => (IReadOnlyList<EquipmentDto>)kv.Value.Select(e => GearMapper.ToDto(e, GearMapper.BuildSourceInfo(e), 0)).ToList()));
            await WriteAsync(Path.Combine("gear", $"{job}.json"), export, ct);
            total += frontier.Sum(kv => kv.Value.Count);
        }

        return total;
    }

    private static IReadOnlyDictionary<string, double> StatWeights(Domain.Models.StatWeightProfile p) =>
        new Dictionary<string, double>
        {
            ["craftsmanship"] = p.CraftsmanshipWeight,
            ["control"] = p.ControlWeight,
            ["cp"] = p.CpWeight,
            ["gathering"] = p.GatheringWeight,
            ["perception"] = p.PerceptionWeight,
            ["gp"] = p.GpWeight,
            ["vitality"] = p.VitalityWeight,
            ["strength"] = p.StrengthWeight,
            ["dexterity"] = p.DexterityWeight,
            ["intelligence"] = p.IntelligenceWeight,
            ["mind"] = p.MindWeight,
            ["piety"] = p.PietyWeight,
            ["tenacity"] = p.TenacityWeight,
            ["directHitRate"] = p.DirectHitWeight,
            ["criticalHit"] = p.CriticalHitWeight,
            ["determination"] = p.DeterminationWeight,
            ["skillSpeed"] = p.SkillSpeedWeight,
            ["spellSpeed"] = p.SpellSpeedWeight,
            ["physicalDamage"] = p.PhysicalDamageWeight,
            ["defense"] = p.DefenseWeight,
            ["magicDefense"] = p.MagicDefenseWeight,
        };

    private async Task ExportSearchIndexAsync(CancellationToken ct)
    {
        Directory.CreateDirectory(Path.Combine(options.OutDir, "search"));

        var equipment = await db.Equipment.AsNoTracking()
            .Select(e => new { e.EquipmentId, e.EquipSlot, e.EquipLevel, e.ClassJobCategory })
            .ToDictionaryAsync(e => e.EquipmentId, e => new SearchIndexEquipment(e.EquipSlot.ToString(), e.EquipLevel, e.ClassJobCategory), ct);
        var recipesByItem = (await db.Recipes.AsNoTracking()
                .Select(r => new { r.ResultItemId, r.RecipeId, r.CraftType })
                .OrderBy(r => r.RecipeId)
                .ToListAsync(ct))
            .GroupBy(r => r.ResultItemId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<SearchIndexRecipe>)g.Select(r => new SearchIndexRecipe(r.RecipeId, r.CraftType.ToString())).ToList());

        var entries = new List<SearchIndexEntry>();
        await foreach (var item in db.Items.AsNoTracking().Where(i => i.Name != "").OrderBy(i => i.ItemId).AsAsyncEnumerable().WithCancellation(ct))
        {
            var dto = Application.Mapping.Mapper.ToDto(item);
            entries.Add(new SearchIndexEntry(
                dto.ItemId, dto.Name, IconUrl.ToId(dto.IconUrl), dto.ItemLevel, dto.Category, dto.CanBeHQ,
                recipesByItem.GetValueOrDefault(item.ItemId) ?? [],
                equipment.GetValueOrDefault(item.ItemId)));
        }

        await WriteAsync(Path.Combine("search", "items.json"), entries, ct);
    }

    private async Task WriteAsync<T>(string relativePath, T value, CancellationToken ct)
    {
        var path = Path.Combine(options.OutDir, relativePath);
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, value, Json, ct);
    }

    private static string SanitizeForPath(string value)
    {
        var chars = value.Select(c => char.IsLetterOrDigit(c) || c == '.' || c == '-' ? c : '-').ToArray();
        return new string(chars).Trim('-');
    }
}
