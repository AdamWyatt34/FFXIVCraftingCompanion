import { describe, expect, it } from "vitest";
import type { RecipeExport } from "../types";
import { toRecipeDto } from "../recipes";
import { iconUrlFromId } from "../icon-url";
import { bucketOf, itemBucketPath, recipeBucketPath } from "../paths";
import { readGoldenLines } from "./goldens";

describe("recipe export", () => {
  const recipes = readGoldenLines<RecipeExport>("recipe-export.jsonl");

  it("returns the API recipe with and without the material summary", () => {
    for (const data of recipes) {
      expect(toRecipeDto(data, true)).toEqual(data.recipe);
      expect(toRecipeDto(data, false)).toEqual({ ...data.recipe, materialSummary: null });
      expect(data.craftingParameters.recipeId).toBe(data.recipe.recipeId);
    }
  });

  it("keeps every tree item in the item table unless it is a crystal", () => {
    for (const { materials } of recipes) {
      const visit = (node: typeof materials.tree): void => {
        const crystal = node.itemId >= 2 && node.itemId <= 19;
        expect(crystal || node.itemId in materials.items).toBe(true);
        node.children.forEach(visit);
      };
      visit(materials.tree);
    }
  });
});

describe("paths", () => {
  it("buckets ids by hundreds", () => {
    expect(bucketOf(35423)).toBe(354);
    expect(bucketOf(99)).toBe(0);
    expect(recipeBucketPath(35423)).toBe("recipes/354.json");
    expect(itemBucketPath(2)).toBe("items/0.json");
  });

  it("rebuilds the XIVAPI icon URL from an id", () => {
    expect(iconUrlFromId(35649)).toBe(
      "https://v2.xivapi.com/api/asset?path=ui%2Ficon%2F035000%2F035649.tex&format=png",
    );
    expect(iconUrlFromId(999)).toBe(
      "https://v2.xivapi.com/api/asset?path=ui%2Ficon%2F000000%2F000999.tex&format=png",
    );
  });
});
