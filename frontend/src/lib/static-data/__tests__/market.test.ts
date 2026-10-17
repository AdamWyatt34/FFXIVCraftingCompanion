import { describe, expect, it } from "vitest";
import { readFileSync } from "node:fs";
import { join } from "node:path";
import type { CostEstimateDto } from "@/types/market";
import type { MaterialItemExport, RecipeExport } from "../types";
import { expandMaterialTree } from "../materials";
import { estimateCost, marketBoardItemIds, parseUniversalisBatch, toApiDateTime, worldGroups, isValidWorldOrDc } from "../market";
import { readGoldenJson, readGoldenLines } from "./goldens";

interface CostGolden { recipeId: number; quantity: number; useMinPrice: boolean; includeGatheredItemPrices: boolean; result: CostEstimateDto }

// FixtureDatabase.Now in the C# harness.
const NOW_MS = Date.UTC(2026, 9, 18, 12, 0, 0);

describe("cost estimate", () => {
  const recipes = new Map(readGoldenLines<RecipeExport>("recipe-export.jsonl").map((r) => [r.recipe.recipeId, r]));
  const crystals = readGoldenJson<Record<string, MaterialItemExport>>("crystals.json");
  const samplePath = join(__dirname, "..", "..", "..", "..", "..", "tests", "FFXIV.Tests", "StaticData", "TestData", "universalis-sample.json");
  const prices = parseUniversalisBatch(JSON.parse(readFileSync(samplePath, "utf8")));

  it.each(readGoldenLines<CostGolden>("cost-estimate.jsonl").map((g) => [`${g.recipeId} x${g.quantity} min=${g.useMinPrice} gathered=${g.includeGatheredItemPrices}`, g] as const))(
    "matches the C# service for %s",
    (_name, golden) => {
      const data = recipes.get(golden.recipeId)!;
      const tree = expandMaterialTree(data.materials, crystals, golden.quantity);
      const materials = [...tree.shoppingList, ...tree.crystalRequirements];
      const options = { useMinPrice: golden.useMinPrice, includeGatheredItemPrices: golden.includeGatheredItemPrices };
      const finalItemId = data.recipe.resultItem.itemId;
      // Only the prices the service would have asked for, as the sample client answered.
      const asked = new Set(marketBoardItemIds(materials, finalItemId, options));
      const available = new Map([...prices].filter(([id]) => asked.has(id)));
      expect(estimateCost(materials, finalItemId, "Gilgamesh", options, available, NOW_MS)).toEqual(golden.result);
    },
  );

  it("formats dates like System.Text.Json", () => {
    expect(toApiDateTime(Date.UTC(2026, 9, 18, 12, 0, 0))).toBe("2026-10-18T12:00:00Z");
    expect(toApiDateTime(Date.UTC(2026, 9, 17, 5, 15, 47, 399))).toBe("2026-10-17T05:15:47.399Z");
    expect(toApiDateTime(Date.UTC(2026, 9, 17, 5, 15, 47, 100))).toBe("2026-10-17T05:15:47.1Z");
  });

  it("serves the world list the API held", () => {
    const groups = worldGroups();
    expect(groups.map((g) => g.dataCenters.length).reduce((a, b) => a + b, 0)).toBe(11);
    expect(groups.flatMap((g) => g.dataCenters).flatMap((dc) => dc.worlds)).toHaveLength(85);
    expect(isValidWorldOrDc("Aether")).toBe(true);
    expect(isValidWorldOrDc("gilgamesh")).toBe(true);
    expect(isValidWorldOrDc("Nowhere")).toBe(false);
  });
});
