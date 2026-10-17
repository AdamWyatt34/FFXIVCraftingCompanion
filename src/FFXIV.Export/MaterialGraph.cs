using FFXIV.Application.Recipes.DTOs;

namespace FFXIV.Export;

// Splits a resolved material tree into a quantity skeleton and a per-item table. Expanding
// the skeleton for a quantity is the frontend's job; the goldens pin the expected result.
public static class MaterialGraph
{
    public const int CrystalMinId = 2;
    public const int CrystalMaxId = 19;

    public static bool IsCrystal(int itemId) => itemId is >= CrystalMinId and <= CrystalMaxId;

    public static MaterialGraphExport Build(MaterialTreeResultDto tree, IDictionary<int, MaterialItemExport>? crystals = null)
    {
        var shopping = tree.ShoppingList.ToDictionary(s => s.ItemId);
        var items = new Dictionary<int, MaterialItemExport>();
        var skeleton = Visit(tree.Tree, items, shopping);

        if (crystals is not null)
        {
            foreach (var crystal in tree.CrystalRequirements)
            {
                var export = new MaterialItemExport(
                    crystal.ItemName, crystal.IconUrl, IsRawMaterial: true, crystal.PrimarySource,
                    crystal.Sources, Recipe: null, crystal.Category, crystal.AlsoAvailableFromVendor);
                lock (crystals)
                {
                    if (crystals.TryGetValue(crystal.ItemId, out var seen) && !Same(seen, export))
                        throw new InvalidOperationException($"Crystal {crystal.ItemId} resolves differently across recipes.");
                    crystals[crystal.ItemId] = export;
                }
            }
        }

        return new MaterialGraphExport(skeleton, items);
    }

    // Items first, skeleton second: whether an occurrence is a cut depends on the item's
    // canonical entry, which may sit anywhere in the tree.
    private static MaterialSkeletonNode Visit(
        MaterialTreeNodeDto root,
        Dictionary<int, MaterialItemExport> items,
        Dictionary<int, ShoppingListItemDto> shopping)
    {
        Collect(root, items, shopping);
        return Skeleton(root, items, perCraft: 1);
    }

    private static void Collect(
        MaterialTreeNodeDto node,
        Dictionary<int, MaterialItemExport> items,
        Dictionary<int, ShoppingListItemDto> shopping)
    {
        // A crafted item can also appear as a raw leaf when the resolver cut a cycle or hit its
        // depth limit; the occurrence with the recipe is the item's canonical entry.
        if (!IsCrystal(node.ItemId)
            && (!items.TryGetValue(node.ItemId, out var existing) || (existing.Recipe is null && node.Recipe is not null)))
        {
            shopping.TryGetValue(node.ItemId, out var listed);
            items[node.ItemId] = new MaterialItemExport(
                node.ItemName, node.IconUrl, node.IsRawMaterial, node.PrimarySource, node.Sources, node.Recipe,
                listed?.Category ?? (node.Recipe is not null ? "Crafted" : node.PrimarySource),
                listed?.AlsoAvailableFromVendor ?? false);
        }

        foreach (var child in node.Children)
            Collect(child, items, shopping);
    }

    private static MaterialSkeletonNode Skeleton(MaterialTreeNodeDto node, Dictionary<int, MaterialItemExport> items, int perCraft)
    {
        // The resolver sized each child as ingredient quantity x crafts of this node; divide the
        // crafts back out so the skeleton holds the recipe's own ingredient quantities.
        var crafts = node.Recipe is null ? 1 : CraftsNeeded(node.Quantity, node.Recipe.Yield);
        var children = node.Children.Select(c =>
        {
            if (c.Quantity % crafts != 0)
                throw new InvalidOperationException($"Item {c.ItemId} under {node.ItemId}: quantity {c.Quantity} is not a multiple of {crafts} crafts.");
            return Skeleton(c, items, c.Quantity / crafts);
        }).ToList();
        var truncated = !IsCrystal(node.ItemId) && node.Recipe is null && items[node.ItemId].Recipe is not null;
        return new MaterialSkeletonNode(node.ItemId, perCraft, children, truncated);
    }

    public static int CraftsNeeded(int quantity, int yield)
    {
        var perCraft = Math.Max(yield, 1);
        return (quantity + perCraft - 1) / perCraft;
    }

    private static bool Same(MaterialItemExport a, MaterialItemExport b) =>
        System.Text.Json.JsonSerializer.Serialize(a, StaticDataExporter.Json) ==
        System.Text.Json.JsonSerializer.Serialize(b, StaticDataExporter.Json);
}
