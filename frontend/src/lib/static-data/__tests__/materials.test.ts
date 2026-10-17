import { describe, expect, it } from "vitest";
import type { MaterialTreeResultDto } from "@/types/api";
import type { MaterialItemExport, RecipeExport } from "../types";
import { compareOrdinalIgnoreCase, craftsNeeded, expandMaterialTree } from "../materials";
import { readGoldenLines, readGoldenJson } from "./goldens";

interface MaterialTreeGolden {
  recipeId: number;
  quantity: number;
  result: MaterialTreeResultDto;
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
