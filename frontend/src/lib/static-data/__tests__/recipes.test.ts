import { describe, expect, it } from "vitest";
import type { RecipeCraftingParametersDto } from "@/types/api";
import type { RecipeExport } from "../types";
import { toRecipeDto, withRequiredStats } from "../recipes";
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

describe("required stats", () => {
  // Recipe 35423 verbatim, kept inline so a re-export of recipes/354.json cannot turn this into a new-shape case.
  const legacyParameters: RecipeCraftingParametersDto = {"recipeId":35423,"recipeLevel":620,"difficulty":5720,"quality":12900,"durability":70,"progressDivider":130,"progressModifier":80,"qualityDivider":115,"qualityModifier":70,"classJobLevel":90,"stars":3,"isExpert":false,"conditionsFlag":0,"suggestedCraftsmanship":3700,"suggestedControl":0,"materialQualityFactor":50,"ingredients":[{"itemId":15,"quantity":5,"canBeHQ":false},{"itemId":17,"quantity":5,"canBeHQ":false},{"itemId":37830,"quantity":2,"canBeHQ":false},{"itemId":37833,"quantity":3,"canBeHQ":false},{"itemId":38932,"quantity":3,"canBeHQ":false},{"itemId":39595,"quantity":3,"canBeHQ":false}]};
  const legacyRecipe = { requiredCraftsmanship: 3700, requiredControl: 0 };

  it("takes the recipe's values from a data set exported without them", () => {
    expect(legacyParameters.requiredCraftsmanship).toBeUndefined();
    expect(withRequiredStats(legacyParameters, legacyRecipe)).toEqual({
      ...legacyParameters,
      requiredCraftsmanship: 3700,
      requiredControl: 0,
    });
  });

  it("keeps the values a data set carries, including a requirement of 0", () => {
    const recipe = { requiredCraftsmanship: 9999, requiredControl: 9999 };
    for (const { craftingParameters } of readGoldenLines<RecipeExport>("recipe-export.jsonl")) {
      expect(craftingParameters.requiredCraftsmanship).toBeTypeOf("number");
      expect(withRequiredStats(craftingParameters, recipe)).toEqual(craftingParameters);
    }
    const none = { ...legacyParameters, requiredCraftsmanship: 0, requiredControl: 0 };
    expect(withRequiredStats(none, legacyRecipe)).toEqual(none);
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
