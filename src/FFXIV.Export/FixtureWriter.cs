using System.Text.Json;
using System.Text.Json.Serialization;
using FFXIV.Domain.Entities;
using FFXIV.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FFXIV.Export;

// The parity tests need a frozen slice of game data: every recipe reachable from a few roots,
// the items they touch, their sources, and the equipment of a couple of jobs. Entities are
// written as-is (no navigations) so the tests can load them straight into a DbContext.
public sealed class FixtureWriter(AppDbContext db, ILogger<FixtureWriter> logger)
{
    public const int EquipmentPerSlot = 12;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public async Task WriteAsync(IReadOnlyList<int> rootRecipeIds, IReadOnlyList<string> jobs, string outPath, CancellationToken ct)
    {
        var recipeIds = new HashSet<int>();
        var itemIds = new HashSet<int>();
        var pending = new Queue<int>(rootRecipeIds);

        while (pending.TryDequeue(out var recipeId))
        {
            if (!recipeIds.Add(recipeId))
                continue;

            var recipe = await db.Recipes.AsNoTracking().Include(r => r.Ingredients).FirstOrDefaultAsync(r => r.RecipeId == recipeId, ct)
                ?? throw new InvalidOperationException($"Fixture recipe {recipeId} does not exist.");
            itemIds.Add(recipe.ResultItemId);

            foreach (var ingredient in recipe.Ingredients)
            {
                itemIds.Add(ingredient.ItemId);
                var subRecipes = await db.Recipes.AsNoTracking()
                    .Where(r => r.ResultItemId == ingredient.ItemId)
                    .Select(r => r.RecipeId)
                    .ToListAsync(ct);
                foreach (var sub in subRecipes)
                    pending.Enqueue(sub);
            }
        }

        // Equipment: the fixture's result items that are gear, plus a bounded pool per job.
        var equipment = await db.Equipment.AsNoTracking().Where(e => itemIds.Contains(e.EquipmentId)).ToListAsync(ct);
        foreach (var job in jobs)
        {
            var pool = await db.Equipment.AsNoTracking()
                .Where(e => e.ClassJobCategory.Contains(job))
                .OrderByDescending(e => e.ItemLevel).ThenBy(e => e.EquipmentId)
                .ToListAsync(ct);
            equipment.AddRange(pool.GroupBy(e => e.EquipSlot).SelectMany(g => g.Take(EquipmentPerSlot)));
        }
        equipment = equipment.DistinctBy(e => e.EquipmentId).OrderBy(e => e.EquipmentId).ToList();
        foreach (var e in equipment)
            itemIds.Add(e.EquipmentId);

        // Recipes used to resolve "used in" links and equipment sources, without widening the closure.
        var extraRecipeIds = equipment.Where(e => e.RecipeId.HasValue).Select(e => e.RecipeId!.Value);
        foreach (var id in extraRecipeIds)
            recipeIds.Add(id);

        var recipes = await db.Recipes.AsNoTracking().Where(r => recipeIds.Contains(r.RecipeId)).OrderBy(r => r.RecipeId).ToListAsync(ct);
        foreach (var r in recipes)
            itemIds.Add(r.ResultItemId);
        var ingredients = await db.RecipeIngredients.AsNoTracking().Where(i => recipeIds.Contains(i.RecipeId)).OrderBy(i => i.RecipeId).ThenBy(i => i.ItemId).ToListAsync(ct);
        foreach (var i in ingredients)
            itemIds.Add(i.ItemId);

        var items = await db.Items.AsNoTracking().Where(i => itemIds.Contains(i.ItemId)).OrderBy(i => i.ItemId).ToListAsync(ct);
        var sources = await db.ItemSources.AsNoTracking().Where(s => itemIds.Contains(s.ItemId)).OrderBy(s => s.ItemSourceId).ToListAsync(ct);
        var gathering = await db.GatheringInfos.AsNoTracking().Where(g => itemIds.Contains(g.ItemId)).OrderBy(g => g.GatheringInfoId).ToListAsync(ct);
        var vendors = await db.VendorInfos.AsNoTracking().Where(v => itemIds.Contains(v.ItemId)).OrderBy(v => v.VendorInfoId).ToListAsync(ct);
        var levels = recipes.Select(r => r.RecipeLevel).Distinct().ToList();
        var recipeLevels = await db.RecipeLevelEntries.AsNoTracking().Where(l => levels.Contains(l.RecipeLevel)).OrderBy(l => l.RecipeLevel).ToListAsync(ct);

        var fixture = new Fixture(rootRecipeIds, jobs, items, recipes, ingredients, recipeLevels, sources, gathering, vendors, equipment);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath))!);
        await using var stream = File.Create(outPath);
        await JsonSerializer.SerializeAsync(stream, fixture, Json, ct);
        await stream.WriteAsync("\n"u8.ToArray(), ct);

        logger.LogInformation("Fixture written to {Path}: {Recipes} recipes, {Items} items, {Equipment} equipment",
            outPath, recipes.Count, items.Count, equipment.Count);
    }

    public sealed record Fixture(
        IReadOnlyList<int> RootRecipeIds,
        IReadOnlyList<string> Jobs,
        IReadOnlyList<Item> Items,
        IReadOnlyList<Recipe> Recipes,
        IReadOnlyList<RecipeIngredient> RecipeIngredients,
        IReadOnlyList<RecipeLevelEntry> RecipeLevels,
        IReadOnlyList<ItemSource> ItemSources,
        IReadOnlyList<GatheringInfo> GatheringInfos,
        IReadOnlyList<VendorInfo> VendorInfos,
        IReadOnlyList<Equipment> Equipment);
}
