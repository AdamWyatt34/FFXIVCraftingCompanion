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

  it("validates like CraftPlansController", () => {
    expect(validatePlanItems([])).toBe("At least one item is required.");
    expect(validatePlanItems(Array.from({ length: 21 }, () => ({ recipeId: 1, quantity: 1 })))).toBe("Maximum of 20 items per plan.");
    expect(validatePlanItems([{ recipeId: 0, quantity: 1 }])).toBe("Invalid recipe ID: 0.");
    expect(validatePlanItems([{ recipeId: 5, quantity: 1000 }])).toBe("Quantity must be between 1 and 999 for recipe 5.");
    expect(validatePlanItems([{ recipeId: 5, quantity: 999 }])).toBeNull();
  });
});
