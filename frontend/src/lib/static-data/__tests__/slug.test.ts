import { describe, expect, it } from "vitest";
import { MAX_SLUG_LENGTH, isCanonicalRecipeSlug, parseRecipeSlug, recipeSlug, slugify } from "../slug";

describe("recipe slugs", () => {
  it("folds item names into URL words", () => {
    expect(slugify("Pure Cosmic Tool-compatible Gel")).toBe("pure-cosmic-tool-compatible-gel");
    expect(slugify("Crème Brûlée")).toBe("creme-brulee");
    expect(slugify("Grade 8 Tincture of Strength")).toBe("grade-8-tincture-of-strength");
    expect(slugify("Y'shtola's Staff")).toBe("yshtolas-staff");
    expect(slugify("  Ice Shard  ")).toBe("ice-shard");
    expect(slugify("★★★")).toBe("");
  });

  it("caps long names at a word boundary and never ends on a hyphen", () => {
    const long = slugify("Augmented Ironworks Magitek Longsword of Everlasting Radiance and Celestial Splendour");
    expect(long.length).toBeLessThanOrEqual(MAX_SLUG_LENGTH);
    expect(long.endsWith("-")).toBe(false);
    expect(long).toBe("augmented-ironworks-magitek-longsword-of-everlasting");
    expect(slugify("a".repeat(100))).toBe("a".repeat(MAX_SLUG_LENGTH));
  });

  it("builds and parses the canonical form", () => {
    expect(recipeSlug(37783, "Pure Cosmic Tool-compatible Gel")).toBe("37783-pure-cosmic-tool-compatible-gel");
    expect(recipeSlug(37783, null)).toBe("37783");
    expect(recipeSlug(37783, "★")).toBe("37783");
    expect(parseRecipeSlug("37783-pure-cosmic-tool-compatible-gel")).toEqual({
      recipeId: 37783,
      nameSlug: "pure-cosmic-tool-compatible-gel",
    });
    expect(parseRecipeSlug("37783")).toEqual({ recipeId: 37783, nameSlug: null });
    expect(parseRecipeSlug("37783-")).toEqual({ recipeId: 37783, nameSlug: "" });
    expect(isCanonicalRecipeSlug("37783-pure-cosmic-tool-compatible-gel", 37783, "Pure Cosmic Tool-compatible Gel")).toBe(true);
    expect(isCanonicalRecipeSlug("37783-pure-cosmic", 37783, "Pure Cosmic Tool-compatible Gel")).toBe(false);
    expect(isCanonicalRecipeSlug("37783", 37783, "Pure Cosmic Tool-compatible Gel")).toBe(false);
  });

  it("rejects everything that is not a recipe URL", () => {
    for (const bad of ["", "abc", "0", "-1", "37783-Pure", "37783_pure", "37783-pure cosmic", "1234567890", "37783/journey", "37783-ünicode"]) {
      expect(parseRecipeSlug(bad), bad).toBeNull();
    }
  });
});
