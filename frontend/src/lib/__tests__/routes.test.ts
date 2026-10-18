import { describe, expect, it } from "vitest";
import { itemPath, recipeJourneyPath, recipePath, rotationPath, simulatorPath } from "../routes";

describe("routes", () => {
  it("builds slug links when the item name is known and bare ids otherwise", () => {
    expect(recipePath(37783, "Pure Cosmic Tool-compatible Gel")).toBe("/recipes/37783-pure-cosmic-tool-compatible-gel");
    expect(recipeJourneyPath(37783, "Pure Cosmic Tool-compatible Gel")).toBe("/recipes/37783-pure-cosmic-tool-compatible-gel/journey");
    expect(recipePath(37783)).toBe("/recipes/37783");
    expect(recipePath(37783, null)).toBe("/recipes/37783");
    expect(recipeJourneyPath(37783)).toBe("/recipes/37783/journey");
  });

  it("builds the other internal links", () => {
    expect(itemPath(5)).toBe("/items/5");
    expect(rotationPath(9)).toBe("/simulator/library/9");
    expect(simulatorPath(37783)).toBe("/simulator?recipeId=37783");
    expect(simulatorPath(37783, { solve: true })).toBe("/simulator?recipeId=37783&solve=true");
  });
});
