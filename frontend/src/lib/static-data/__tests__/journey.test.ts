import { describe, expect, it } from "vitest";
import type { CraftingJourneyDto } from "@/types/journey";
import type { ItemExport, MaterialItemExport, RecipeExport } from "../types";
import { composeJourney } from "../journey";
import { readGoldenJson, readGoldenLines } from "./goldens";

interface JourneyGolden {
  recipeId: number;
  result: CraftingJourneyDto;
}

describe("crafting journey", () => {
  const recipes = new Map(readGoldenLines<RecipeExport>("recipe-export.jsonl").map((r) => [r.recipe.recipeId, r]));
  const items = new Map(readGoldenLines<ItemExport>("item-export.jsonl").map((i) => [i.item.itemId, i]));
  const crystals = readGoldenJson<Record<string, MaterialItemExport>>("crystals.json");

  it.each(readGoldenLines<JourneyGolden>("journey.jsonl").map((g) => [g.recipeId, g] as const))(
    "matches the C# handler for recipe %s",
    (_id, golden) => {
      const data = recipes.get(golden.recipeId)!;
      const context = items.get(data.recipe.resultItem.itemId)!.context;
      expect(composeJourney(data, context, crystals)).toEqual(golden.result);
    },
  );
});
