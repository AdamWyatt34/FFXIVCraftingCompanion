import { describe, expect, it } from "vitest";
import {
  CRAFT_JOBS,
  LEVEL_BRACKETS,
  bracketFor,
  countByJob,
  findBracket,
  groupByLevel,
  hubParams,
  jobSlug,
  parseJobSlug,
  recipesForJob,
  recipesInBracket,
  sortForHub,
  type HubRecipe,
} from "../hubs";

function recipe(recipeId: number, craftType: string, classJobLevel: number, stars = 0, itemName = `Item ${recipeId}`): HubRecipe {
  return { recipeId, itemName, iconUrl: "", itemLevel: 1, craftType, classJobLevel, stars };
}

describe("recipe hubs", () => {
  it("maps jobs to lower-case segments and back, and nothing else", () => {
    for (const job of CRAFT_JOBS) expect(parseJobSlug(jobSlug(job))).toBe(job);
    expect(jobSlug("CRP")).toBe("crp");
    for (const bad of ["CRP", "Crp", "crp ", "xyz", "", "1-50"]) expect(parseJobSlug(bad), bad).toBeNull();
  });

  it("puts every level in exactly one bracket", () => {
    expect(bracketFor(1).slug).toBe("1-50");
    expect(bracketFor(50).slug).toBe("1-50");
    expect(bracketFor(51).slug).toBe("51-60");
    expect(bracketFor(90).slug).toBe("81-90");
    expect(bracketFor(91).slug).toBe("91-100");
    expect(bracketFor(100).slug).toBe("91-100");
    expect(bracketFor(0).slug).toBe("1-50");
    expect(bracketFor(120).slug).toBe("91-100");
    for (const bracket of LEVEL_BRACKETS) expect(findBracket(bracket.slug)).toBe(bracket);
    expect(findBracket("1-999")).toBeNull();
    expect(hubParams()).toHaveLength(CRAFT_JOBS.length * LEVEL_BRACKETS.length);
  });

  it("filters, sorts and groups for a hub page", () => {
    const recipes = [
      recipe(5, "CUL", 100, 3, "Zebra Stew"),
      recipe(4, "CUL", 100, 3, "Apple Pie"),
      recipe(3, "CUL", 100, 1, "Zinc Soup"),
      recipe(2, "CUL", 91, 0),
      recipe(1, "ARM", 90, 3),
      recipe(6, "CUL", 50, 0),
    ];
    const cul = recipesForJob(recipes, "CUL");
    expect(cul.map((r) => r.recipeId)).toEqual([5, 4, 3, 2, 6]);
    const latest = sortForHub(recipesInBracket(cul, findBracket("91-100")!));
    expect(latest.map((r) => r.recipeId)).toEqual([2, 3, 4, 5]);
    expect(groupByLevel(latest).map((g) => [g.level, g.recipes.length])).toEqual([[91, 1], [100, 3]]);
    expect(countByJob(recipes)).toMatchObject({ CUL: 5, ARM: 1, CRP: 0 });
  });
});
