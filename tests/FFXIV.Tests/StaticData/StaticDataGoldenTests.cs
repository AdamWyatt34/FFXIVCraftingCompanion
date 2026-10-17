using System.Text;
using System.Text.Json;
using FFXIV.Application.Items.Queries;
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
            text.Append(JsonSerializer.Serialize(new RecipeExport(recipe!, parameters!, materials!), StaticDataExporter.Json)).Append('\n');
        }

        AssertOrWrite("recipe-export.jsonl", text.ToString());
    }

    [Fact]
    public async Task Item_export_golden_is_current()
    {
        using var scope = _db.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var gatheringRepo = scope.ServiceProvider.GetRequiredService<IGatheringInfoRepository>();
        var text = new StringBuilder();

        var itemIds = _db.Fixture.Items.Select(i => i.ItemId).OrderBy(id => id).ToList();
        var gathering = await gatheringRepo.GetByItemIdsAsync(itemIds);
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
            text.Append(JsonSerializer.Serialize(new ItemExport(item!, context!, nodes), StaticDataExporter.Json)).Append('\n');
        }

        AssertOrWrite("item-export.jsonl", text.ToString());
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
}
