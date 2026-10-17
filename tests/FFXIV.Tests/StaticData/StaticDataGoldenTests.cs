using System.Text;
using System.Text.Json;
using FFXIV.Application.Gathering;
using FFXIV.Application.Gathering.DTOs;
using FFXIV.Application.Gear;
using FFXIV.Application.Market.DTOs;
using FFXIV.Application.Market.Queries;
using FFXIV.Domain.Models;
using FFXIV.Domain.Utilities;
using FFXIV.Application.Gear.DTOs;
using FFXIV.Application.Gear.Queries;
using FFXIV.Application.CraftPlans.DTOs;
using FFXIV.Application.CraftPlans.Queries;
using FFXIV.Application.DTOs;
using FFXIV.Application.Journey.DTOs;
using FFXIV.Application.Items.Queries;
using FFXIV.Domain.Enums;
using FFXIV.Application.Journey.Queries;
using FFXIV.Application.Recipes.DTOs;
using FFXIV.Application.Recipes.Queries;
using FFXIV.Domain.Repositories;
using FFXIV.Export;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace FFXIV.Tests.StaticData;

// Goldens for the computations the frontend replays from static data. Same contract as the
// engine parity goldens: FFXIV_PARITY_GENERATE=1 rewrites them, otherwise the first differing
// byte fails. Regenerate only when a C# handler change is intended.
public class StaticDataGoldenTests : IClassFixture<FixtureDatabase>
{
    private const string GenerateVariable = "FFXIV_PARITY_GENERATE";
    private static readonly int[] Quantities = [1, 2, 7, 99, 999];

    private static readonly (string? Query, string? CraftType, int Page, int PageSize)[] Searches =
    [
        ("ing", null, 1, 20),
        ("ing", null, 2, 20),
        ("ing", null, 3, 5),
        ("Ind", null, 1, 20),
        ("INGOT", null, 1, 50),
        ("ilmenite ingot", null, 1, 20),
        ("Grade 8", null, 1, 20),
        ("zzzz", null, 1, 20),
        ("o'", null, 1, 20),
        (null, "ARM", 1, 20),
        (null, "CRP", 2, 10),
        ("ingot", "ARM", 1, 20),
        ("ingot", "BSM", 1, 20),
        ("saw", "CRP", 1, 20),
    ];

    private static readonly string GoldenDirectory =
        Path.Combine(AppContext.BaseDirectory, "StaticData", "TestData", "goldens");

    private readonly FixtureDatabase _db;

    public StaticDataGoldenTests(FixtureDatabase db)
    {
        _db = db;
    }

    [Fact]
    public async Task Recipe_export_golden_is_current()
    {
        using var scope = _db.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var text = new StringBuilder();

        foreach (var recipeId in _db.Fixture.RootRecipeIds)
        {
            var recipe = await mediator.Send(new GetRecipeByIdQuery(recipeId, IncludeMaterials: true));
            var parameters = await mediator.Send(new GetRecipeCraftingParametersQuery(recipeId));
            var materials = await mediator.Send(new GetMaterialTreeQuery(recipeId, Quantity: 1));
            recipe.Should().NotBeNull();
            parameters.Should().NotBeNull();
            materials.Should().NotBeNull();
            text.Append(JsonSerializer.Serialize(new RecipeExport(recipe!, parameters!, MaterialGraph.Build(materials!)), StaticDataExporter.Json)).Append('\n');
        }

        AssertOrWrite("recipe-export.jsonl", text.ToString());
    }

    [Fact]
    public async Task Item_export_golden_is_current()
    {
        using var scope = _db.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var gatheringRepo = scope.ServiceProvider.GetRequiredService<IGatheringInfoRepository>();
        var equipmentRepo = scope.ServiceProvider.GetRequiredService<IEquipmentRepository>();
        var text = new StringBuilder();

        var itemIds = _db.Fixture.Items.Select(i => i.ItemId).OrderBy(id => id).ToList();
        var gathering = await gatheringRepo.GetByItemIdsAsync(itemIds);
        var equipment = (await equipmentRepo.GetByIdsAsync(itemIds)).ToDictionary(e => e.EquipmentId);
        foreach (var itemId in itemIds)
        {
            var item = await mediator.Send(new GetItemByIdQuery(itemId));
            var context = await mediator.Send(new GetItemContextQuery(itemId));
            item.Should().NotBeNull();
            context.Should().NotBeNull();
            var nodes = gathering.TryGetValue(itemId, out var rows)
                ? rows.OrderBy(g => g.GatheringInfoId).Select(g => new GatheringNodeExport(
                    g.GatheringType, g.ClassJob, g.Level, g.Stars, g.ZoneName, g.AreaName,
                    g.CoordinateX, g.CoordinateY, g.IsTimed, g.SpawnTimes, g.IsHidden, g.NodeType)).ToList()
                : [];
            var gear = equipment.TryGetValue(itemId, out var piece) ? GearMapper.ToDto(piece, GearMapper.BuildSourceInfo(piece), 0) : null;
            text.Append(JsonSerializer.Serialize(new ItemExport(item!, context!, nodes, gear), StaticDataExporter.Json)).Append('\n');
        }

        AssertOrWrite("item-export.jsonl", text.ToString());
    }

    [Fact]
    public async Task Item_search_golden_is_current()
    {
        using var scope = _db.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var text = new StringBuilder();

        foreach (var (query, craftType, page, pageSize) in Searches)
        {
            var parsed = craftType is null ? (CraftType?)null : Enum.Parse<CraftType>(craftType);
            var result = await mediator.Send(new SearchItemsQuery(query, page, pageSize, parsed));
            text.Append(JsonSerializer.Serialize(new ItemSearchGolden(query, craftType, page, pageSize, result), StaticDataExporter.Json)).Append('\n');
        }

        AssertOrWrite("item-search.jsonl", text.ToString());
    }

    private static readonly (int RecipeId, int Quantity)[][] Plans =
    [
        [(35423, 1)],
        [(35423, 3), (35585, 2)],
        [(34065, 999)],
        [(2240, 7), (35423, 1), (35585, 1), (34065, 2)],
    ];

    [Fact]
    public async Task Crystals_golden_is_current()
    {
        using var scope = _db.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var crystals = new Dictionary<int, MaterialItemExport>();

        foreach (var recipeId in _db.Fixture.RootRecipeIds)
        {
            var materials = await mediator.Send(new GetMaterialTreeQuery(recipeId, Quantity: 1));
            MaterialGraph.Build(materials!, crystals);
        }

        AssertOrWrite("crystals.json", JsonSerializer.Serialize(new SortedDictionary<int, MaterialItemExport>(crystals), StaticDataExporter.Json) + "\n");
    }

    [Fact]
    public async Task Craft_plan_golden_is_current()
    {
        using var scope = _db.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var text = new StringBuilder();

        foreach (var plan in Plans)
        {
            var items = plan.Select(p => new CraftPlanItemInput { RecipeId = p.RecipeId, Quantity = p.Quantity }).ToList();
            var result = await mediator.Send(new ResolveCraftPlanQuery(items));
            result.Should().NotBeNull();
            var exports = new Dictionary<string, string>();
            foreach (var format in new[] { "text", "json", "teamcraft" })
            {
                var export = await mediator.Send(new ExportCraftPlanQuery(items, format));
                exports[format] = export!.Content;
            }
            text.Append(JsonSerializer.Serialize(new CraftPlanGolden(items, result!, exports), StaticDataExporter.Json)).Append('\n');
        }

        AssertOrWrite("craft-plan.jsonl", text.ToString());
    }

    [Fact]
    public async Task Journey_golden_is_current()
    {
        using var scope = _db.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var text = new StringBuilder();

        foreach (var recipeId in _db.Fixture.RootRecipeIds)
        {
            var (result, error) = await mediator.Send(new GetCraftingJourneyQuery(recipeId, UserId: null));
            error.Should().BeNull();
            text.Append(JsonSerializer.Serialize(new JourneyGolden(recipeId, result!), StaticDataExporter.Json)).Append('\n');
        }

        AssertOrWrite("journey.jsonl", text.ToString());
    }

    private static readonly (string Job, int Level, string Profile, bool CraftedOnly)[] GearSets =
    [
        ("CRP", 100, "balanced", false),
        ("CRP", 100, "Balanced", true),
        ("CRP", 90, "progressfocused", false),
        ("CRP", 100, "cpfocused", true),
        ("CRP", 100, "gatheringbalanced", false),
        ("CRP", 100, "nonsense", false),
        ("CRP", 1, "balanced", false),
        ("MIN", 100, "gatheringbalanced", false),
        ("MIN", 100, "perceptionfocused", false),
        ("MIN", 50, "balanced", false),
        ("MIN", 100, "balanced", true),
    ];

    private static readonly (string Query, string? Job, string? Slot, int? MinLevel, int? MaxLevel)[] GearSearches =
    [
        ("saw", null, null, null, null),
        ("of Crafting", "CRP", null, null, null),
        ("of crafting", "CRP", "Head", null, null),
        ("", "MIN", "MainHand", null, null),
        ("", "MIN", null, 80, 99),
        ("ING", null, null, 100, null),
        ("zzzz", null, null, null, null),
        ("", "CRP", null, null, null),
    ];

    [Fact]
    public async Task Gear_export_golden_is_current()
    {
        using var scope = _db.CreateScope();
        var equipmentRepo = scope.ServiceProvider.GetRequiredService<IEquipmentRepository>();
        var itemSourceRepo = scope.ServiceProvider.GetRequiredService<IItemSourceRepository>();
        var gearService = scope.ServiceProvider.GetRequiredService<IGearRecommendationService>();
        var text = new StringBuilder();

        foreach (var job in _db.Fixture.Jobs)
        {
            var export = await GearExport.BuildJobAsync(job, equipmentRepo, itemSourceRepo, gearService, CancellationToken.None);
            text.Append(JsonSerializer.Serialize(export, StaticDataExporter.Json)).Append('\n');
        }

        AssertOrWrite("gear-export.jsonl", text.ToString());
        AssertOrWrite("gear-profiles.json", JsonSerializer.Serialize(GearExport.AllProfiles(), StaticDataExporter.Json) + "\n");
    }

    [Fact]
    public async Task Gear_sets_golden_is_current()
    {
        using var scope = _db.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var text = new StringBuilder();

        foreach (var (job, level, profile, craftedOnly) in GearSets)
        {
            var result = await mediator.Send(new GetGearRecommendationsQuery(job, level, profile, craftedOnly));
            text.Append(JsonSerializer.Serialize(new GearSetGolden(job, level, profile, craftedOnly, result), StaticDataExporter.Json)).Append('\n');
        }
        AssertOrWrite("gear-sets.jsonl", text.ToString());

        var lists = new StringBuilder();
        foreach (var job in new string?[] { null, "CRP", "MIN", "pld", "WHM", "NIN", "BRD", "BLM" })
        {
            var result = await mediator.Send(new GetStatWeightProfilesQuery(job));
            lists.Append(JsonSerializer.Serialize(new ProfileListGolden(job, result), StaticDataExporter.Json)).Append('\n');
        }
        AssertOrWrite("gear-profile-lists.jsonl", lists.ToString());

        var searches = new StringBuilder();
        foreach (var (query, job, slot, minLevel, maxLevel) in GearSearches)
        {
            var result = await mediator.Send(new SearchEquipmentQuery(query, job, slot, minLevel, maxLevel));
            searches.Append(JsonSerializer.Serialize(new GearSearchGolden(query, job, slot, minLevel, maxLevel, result), StaticDataExporter.Json)).Append('\n');
        }
        AssertOrWrite("gear-search.jsonl", searches.ToString());
    }

    private static readonly int[] GuideHours = [0, 5, 12, 23];

    [Fact]
    public void Eorzean_time_golden_is_current()
    {
        var hours = new long[] { 0, 1_000_000_000, 1_760_724_000, 1_760_724_174, 1_760_724_175, 1_760_728_199, 2_147_483_647 }
            .Select(seconds => new EorzeanHourGolden(seconds, EorzeanTime.GetEorzeanHour(DateTimeOffset.FromUnixTimeSeconds(seconds))))
            .ToList();

        var windows = new List<WindowGolden>();
        foreach (var (start, end) in new[] { (0, 0), (2, 6), (22, 2), (10, 12), (23, 0), (5, 5) })
            foreach (var current in new[] { 0, 1, 2, 5, 6, 10, 11, 12, 21, 22, 23 })
            {
                var until = EorzeanTime.TimeUntilWindow(start, end, current);
                windows.Add(new WindowGolden(start, end, current,
                    EorzeanTime.IsWindowActive(start, end, current),
                    (long)until.TotalSeconds,
                    GatheringGuideMapper.FormatTimeUntil(until)));
            }

        AssertOrWrite("eorzean-time.json", JsonSerializer.Serialize(new EorzeanGolden(hours, windows), StaticDataExporter.Json) + "\n");
    }

    [Fact]
    public async Task Gathering_guide_golden_is_current()
    {
        using var scope = _db.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var service = scope.ServiceProvider.GetRequiredService<IGatheringGuideService>();
        var text = new StringBuilder();

        var inputs = new List<(string Name, List<GatheredMaterialInput> Items)>();
        var everything = new List<GatheredMaterialInput>();
        foreach (var recipeId in _db.Fixture.RootRecipeIds)
        {
            // What the journey page sends: gathered raw materials plus crystals.
            var tree = await mediator.Send(new GetMaterialTreeQuery(recipeId, Quantity: 3));
            var items = tree!.ShoppingList.Where(s => s.PrimarySource == "Gathered").Concat(tree.CrystalRequirements)
                .Select(s => new GatheredMaterialInput(s.ItemId, s.TotalQuantity)).ToList();
            inputs.Add(($"recipe-{recipeId}", items));
            everything.AddRange(items.Where(i => everything.All(e => e.ItemId != i.ItemId)));
        }
        inputs.Add(("all-roots", everything));
        inputs.Add(("mixed", [new(2, 5), new(999999, 1), new(_db.Fixture.Items[0].ItemId, 2), new(14, 1)]));
        inputs.Add(("empty", []));

        foreach (var (name, items) in inputs)
        {
            var guide = await service.GenerateGuideAsync(items);
            foreach (var hour in GuideHours)
                text.Append(JsonSerializer.Serialize(new GatheringGuideGolden(name, items, hour, GatheringGuideMapper.ToDto(guide, hour)), StaticDataExporter.Json)).Append('\n');
        }

        AssertOrWrite("gathering-guide.jsonl", text.ToString());
    }

    [Fact]
    public async Task Cost_estimate_golden_is_current()
    {
        using var scope = _db.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var text = new StringBuilder();

        var optionSets = new (bool UseMin, bool Gathered)[] { (false, true), (true, true), (false, false), (true, false) };
        foreach (var recipeId in _db.Fixture.RootRecipeIds)
            foreach (var quantity in new[] { 1, 3 })
                foreach (var (useMin, gathered) in optionSets)
                {
                    var options = new CostEstimationOptions { UseMinPrice = useMin, IncludeGatheredItemPrices = gathered };
                    var result = await mediator.Send(new GetRecipeCostQuery(recipeId, quantity, "Gilgamesh", options));
                    result.Should().NotBeNull();
                    text.Append(JsonSerializer.Serialize(new CostGolden(recipeId, quantity, useMin, gathered, result!), StaticDataExporter.Json)).Append('\n');
                }

        AssertOrWrite("cost-estimate.jsonl", text.ToString());
    }

    [Fact]
    public async Task Material_tree_quantities_golden_is_current()
    {
        using var scope = _db.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var text = new StringBuilder();

        foreach (var recipeId in _db.Fixture.RootRecipeIds)
        {
            foreach (var quantity in Quantities)
            {
                var result = await mediator.Send(new GetMaterialTreeQuery(recipeId, quantity));
                result.Should().NotBeNull();
                text.Append(JsonSerializer.Serialize(new MaterialTreeGolden(recipeId, quantity, result!), StaticDataExporter.Json)).Append('\n');
            }
        }

        AssertOrWrite("material-tree.jsonl", text.ToString());
    }

    private static void AssertOrWrite(string fileName, string content)
    {
        var path = Path.Combine(GoldenDirectory, fileName);
        if (Environment.GetEnvironmentVariable(GenerateVariable) == "1")
        {
            Directory.CreateDirectory(GoldenDirectory);
            File.WriteAllText(path, content, new UTF8Encoding(false));
            File.WriteAllText(SourcePath(path), content, new UTF8Encoding(false));
            return;
        }

        File.Exists(path).Should().BeTrue($"{fileName} must be generated with {GenerateVariable}=1");
        var expected = File.ReadAllText(path);
        var firstDifference = FirstDifference(expected, content);
        firstDifference.Should().Be(-1,
            $"{fileName} differs at offset {firstDifference}; regenerate with {GenerateVariable}=1 if the change is intended");
    }

    // Goldens live in the source tree; the copy in the output directory is what the test reads.
    private static string SourcePath(string outputPath)
    {
        var relative = Path.GetRelativePath(AppContext.BaseDirectory, outputPath);
        var source = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", relative));
        Directory.CreateDirectory(Path.GetDirectoryName(source)!);
        return source;
    }

    private static int FirstDifference(string a, string b)
    {
        var length = Math.Min(a.Length, b.Length);
        for (var i = 0; i < length; i++)
            if (a[i] != b[i])
                return i;
        return a.Length == b.Length ? -1 : length;
    }

    private sealed record MaterialTreeGolden(int RecipeId, int Quantity, MaterialTreeResultDto Result);

    private sealed record ItemSearchGolden(string? Query, string? CraftType, int Page, int PageSize, ItemSearchResultDto Result);

    private sealed record CraftPlanGolden(List<CraftPlanItemInput> Items, CraftPlanResultDto Result, Dictionary<string, string> Exports);

    private sealed record JourneyGolden(int RecipeId, CraftingJourneyDto Result);

    private sealed record GearSetGolden(string Job, int Level, string Profile, bool CraftedOnly, GearSetDto Result);

    private sealed record ProfileListGolden(string? Job, List<StatWeightProfileDto> Result);

    private sealed record GearSearchGolden(string Query, string? Job, string? Slot, int? MinLevel, int? MaxLevel, List<EquipmentDto> Result);

    private sealed record EorzeanGolden(List<EorzeanHourGolden> Hours, List<WindowGolden> Windows);

    private sealed record EorzeanHourGolden(long UnixSeconds, int Hour);

    private sealed record WindowGolden(int StartHour, int EndHour, int CurrentHour, bool IsActive, long SecondsUntil, string TimeUntil);

    private sealed record GatheringGuideGolden(string Name, List<GatheredMaterialInput> Items, int CurrentHour, GatheringGuideDto Result);

    private sealed record CostGolden(int RecipeId, int Quantity, bool UseMinPrice, bool IncludeGatheredItemPrices, CostEstimateDto Result);
}
