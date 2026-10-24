import { describe, expect, it } from "vitest";
import type { CraftPlanItemInput, CraftPlanResultDto } from "@/types/craft-plan";
import type { MaterialItemExport, RecipeExport } from "../types";
import { aggregateCraftPlan, exportCraftPlanContent, validatePlanItems, type ExportFormat } from "../craft-plans";
import { expandMaterialTree } from "../materials";
import { readGoldenJson, readGoldenLines } from "./goldens";

interface CraftPlanGolden {
  items: CraftPlanItemInput[];
  result: CraftPlanResultDto;
  exports: Record<ExportFormat, string>;
}

describe("craft plan resolution", () => {
  const recipes = new Map(readGoldenLines<RecipeExport>("recipe-export.jsonl").map((r) => [r.recipe.recipeId, r]));
  const crystals = readGoldenJson<Record<string, MaterialItemExport>>("crystals.json");
  const cases = readGoldenLines<CraftPlanGolden>("craft-plan.jsonl");

  const resolve = (items: CraftPlanItemInput[]) =>
    aggregateCraftPlan(
      items.map((input) => {
        const data = recipes.get(input.recipeId)!;
        return { input, recipe: data.recipe, tree: expandMaterialTree(data.materials, crystals, input.quantity) };
      }),
    );

  it.each(cases.map((c) => [c.items.map((i) => `${i.recipeId}x${i.quantity}`).join("+"), c] as const))(
    "matches the C# resolver for plan %s",
    (_name, golden) => {
      expect(resolve(golden.items)).toEqual(golden.result);
    },
  );

  it.each(cases.map((c) => [c.items.map((i) => `${i.recipeId}x${i.quantity}`).join("+"), c] as const))(
    "exports plan %s as text, json and teamcraft like the API",
    (_name, golden) => {
      const result = resolve(golden.items);
      for (const format of ["text", "json", "teamcraft"] as const) {
        expect(exportCraftPlanContent(result, format)).toBe(golden.exports[format]);
      }
    },
  );

  it("crafts an intermediate two plan items share once for both", () => {
    // Each Grade 8 Tincture craft (yield 3) needs 1 Grade 5 Strength Alkahest (yield 3, per craft
    // 8 Water Crystal and 2 Sweet Alyssum). x2 is 1 craft and x4 is 2: 3 Alkahest, one craft.
    const result = resolve([{ recipeId: 35585, quantity: 2 }, { recipeId: 35585, quantity: 4 }]);
    expect(result.intermediateCrafts).toMatchObject([{ itemName: "Grade 5 Strength Alkahest", totalQuantity: 3, yieldPerCraft: 3 }]);
    expect(result.crystals.find((c) => c.itemName === "Water Crystal")?.totalQuantity).toBe(8);
    const alyssum = result.rawMaterials.find((m) => m.itemName === "Sweet Alyssum")!;
    expect(alyssum.totalQuantity).toBe(2);
    // The second plan item's Alkahest comes out of the first one's craft.
    expect(alyssum.contributions.map((c) => c.quantity)).toEqual([2, 0]);
  });

  it("validates like CraftPlansController", () => {
    expect(validatePlanItems([])).toBe("At least one item is required.");
    expect(validatePlanItems(Array.from({ length: 21 }, () => ({ recipeId: 1, quantity: 1 })))).toBe("Maximum of 20 items per plan.");
    expect(validatePlanItems([{ recipeId: 0, quantity: 1 }])).toBe("Invalid recipe ID: 0.");
    expect(validatePlanItems([{ recipeId: 5, quantity: 1000 }])).toBe("Quantity must be between 1 and 999 for recipe 5.");
    expect(validatePlanItems([{ recipeId: 5, quantity: 999 }])).toBeNull();
  });
});
