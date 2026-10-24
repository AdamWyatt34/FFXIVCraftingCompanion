import { describe, expect, it } from "vitest";
import type { MaterialTreeResultDto, RecipeSummaryDto } from "@/types/api";
import type { MaterialGraphExport, MaterialItemExport, MaterialSkeletonNode, RecipeExport } from "../types";
import { compareOrdinalIgnoreCase, craftsNeeded, expandMaterialTree } from "../materials";
import { readGoldenLines, readGoldenJson } from "./goldens";
import { buildIngredientMap } from "@/lib/tree-utils";

interface MaterialTreeGolden {
  recipeId: number;
  quantity: number;
  result: MaterialTreeResultDto;
}

// Root needs A and B; each needs 1 Intermediate (yield 3), which needs 1 Raw and 2 Fire Crystals.
function sharedIntermediateGraph(): { graph: MaterialGraphExport; crystals: Record<string, MaterialItemExport> } {
  const recipe = (recipeId: number, yieldPerCraft: number): RecipeSummaryDto => ({
    recipeId, craftType: "ALC", craftTypeName: "Alchemist", recipeLevel: 1, requiredCraftsmanship: 0, requiredControl: 0, yield: yieldPerCraft,
  });
  const item = (itemName: string, crafted: RecipeSummaryDto | null): MaterialItemExport => ({
    itemName, iconUrl: "", isRawMaterial: crafted === null, primarySource: crafted ? "Crafted" : "Gathered", sources: [],
    recipe: crafted, category: crafted ? "Crafted" : "Gathered", alsoAvailableFromVendor: false,
  });
  const node = (itemId: number, perCraft: number, children: MaterialSkeletonNode[] = []): MaterialSkeletonNode => ({
    itemId, perCraft, children, truncated: false,
  });
  const intermediate = () => node(710, 1, [node(720, 1), node(8, 2)]);
  return {
    graph: {
      tree: node(700, 1, [node(701, 1, [intermediate()]), node(702, 1, [intermediate()])]),
      items: {
        "700": item("Root", recipe(1, 1)),
        "701": item("Branch A", recipe(2, 1)),
        "702": item("Branch B", recipe(3, 1)),
        "710": item("Intermediate", recipe(4, 3)),
        "720": item("Raw", null),
      },
    },
    crystals: { "8": { ...item("Fire Crystal", null), category: "Crystals" } },
  };
}

describe("material tree expansion", () => {
  const recipes = new Map(readGoldenLines<RecipeExport>("recipe-export.jsonl").map((r) => [r.recipe.recipeId, r]));
  const crystals = readGoldenJson<Record<string, MaterialItemExport>>("crystals.json");
  const cases = readGoldenLines<MaterialTreeGolden>("material-tree.jsonl");

  it.each(cases.map((c) => [`${c.recipeId} x${c.quantity}`, c] as const))(
    "matches the C# resolver for recipe %s",
    (_name, golden) => {
      const data = recipes.get(golden.recipeId)!;
      expect(expandMaterialTree(data.materials, crystals, golden.quantity)).toEqual(golden.result);
    },
  );

  it("crafts a shared intermediate once for both branches", () => {
    // Two Intermediates in all: one craft, so 1 Raw and 2 crystals, not 2 and 4.
    const { graph, crystals: table } = sharedIntermediateGraph();
    const result = expandMaterialTree(graph, table, 1);
    expect(result.intermediateCrafts.find((c) => c.itemId === 710)?.quantity).toBe(2);
    expect(result.shoppingList.find((s) => s.itemId === 720)?.totalQuantity).toBe(1);
    expect(result.crystalRequirements.find((c) => c.itemId === 8)?.totalQuantity).toBe(2);
    // The tree keeps what each branch alone would need.
    expect(result.tree.children[1].children[0].children[0]).toMatchObject({ itemId: 720, quantity: 1 });
  });

  it("applies the quantity before rounding a shared intermediate", () => {
    // 4 roots: 8 Intermediates, ceil(8 / 3) = 3 crafts and 3 Raw (2 + 2 branch by branch).
    const { graph, crystals: table } = sharedIntermediateGraph();
    const result = expandMaterialTree(graph, table, 4);
    expect(result.intermediateCrafts.find((c) => c.itemId === 710)?.quantity).toBe(8);
    expect(result.shoppingList.find((s) => s.itemId === 720)?.totalQuantity).toBe(3);
    expect(result.crystalRequirements.find((c) => c.itemId === 8)?.totalQuantity).toBe(6);
  });

  it("gives the crafting steps per-craft ingredients to scale by the step's total crafts", () => {
    // x4: 8 Intermediates are 3 crafts, so the step needs 3 Raw; one branch's node holds 2.
    const { graph, crystals: table } = sharedIntermediateGraph();
    const result = expandMaterialTree(graph, table, 4);
    const recipe = buildIngredientMap(result.tree).get(710)!;
    const crafts = craftsNeeded(result.intermediateCrafts.find((c) => c.itemId === 710)!.quantity, recipe.yieldPerCraft);
    expect(crafts).toBe(3);
    expect(recipe.ingredients.map(({ node, perCraft }) => [node.itemId, perCraft * crafts])).toEqual([[720, 3], [8, 6]]);
  });

  it("rounds crafts up by yield", () => {
    expect(craftsNeeded(1, 3)).toBe(1);
    expect(craftsNeeded(3, 3)).toBe(1);
    expect(craftsNeeded(4, 3)).toBe(2);
    expect(craftsNeeded(7, 0)).toBe(7);
  });

  it("compares names like StringComparison.OrdinalIgnoreCase", () => {
    expect(compareOrdinalIgnoreCase("apple", "Banana")).toBeLessThan(0);
    expect(compareOrdinalIgnoreCase("Zinc", "aether")).toBeGreaterThan(0);
    expect(compareOrdinalIgnoreCase("Ore", "ore")).toBe(0);
    // Uppercase letters sort before the underscore in code unit order, which OrdinalIgnoreCase keeps.
    expect(compareOrdinalIgnoreCase("a_b", "ab")).toBeGreaterThan(0);
  });
});
