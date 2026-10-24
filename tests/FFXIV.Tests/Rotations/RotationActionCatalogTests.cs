using System.Text.Json;
using FFXIV.Application.Rotations;

namespace FFXIV.Tests.Rotations;

public sealed class RotationActionCatalogTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public void Catalog_matches_the_engine_action_table()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "FFXIVCraftingCompanion.slnx")))
            root = root.Parent;

        if (root is null)
        {
            Environment.GetEnvironmentVariable("CI").Should().BeNullOrEmpty("CI must run from a source checkout");
            return;
        }

        var path = Path.Combine(root.FullName, "crates", "xiv-craft", "tests", "goldens", "actions.json");
        var expected = JsonSerializer.Deserialize<RotationActionEntry[]>(File.ReadAllText(path),
            JsonOptions);
        RotationActionCatalog.All(includeLegacy: true).Should().BeEquivalentTo(expected);
        RotationActionCatalog.All().Should().OnlyContain(action => !action.IsLegacy);
        foreach (var action in expected!)
            RotationActionCatalog.GetById(action.Id).Should().Be(action);
        RotationActionCatalog.GetById(-1).Should().BeNull();
    }
}
