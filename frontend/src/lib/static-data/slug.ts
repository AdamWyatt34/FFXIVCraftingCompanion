// URL slugs for recipe pages: /recipes/{id}-{item-name}. The id is the identity; the name part
// is for people and search engines, and a stale or missing name is redirected to the current one.

/** Slugs are capped so the longest item names still give readable URLs. */
export const MAX_SLUG_LENGTH = 60;

const RECIPE_SLUG = /^(\d{1,9})(?:-([a-z0-9-]*))?$/;

/**
 * ASCII-folds (accents dropped), lowercases, turns every run of other characters into one
 * hyphen, and trims. "Crème Brûlée" becomes "creme-brulee"; apostrophes vanish rather than
 * splitting a word ("Y'shtola" is "yshtola").
 */
export function slugify(text: string): string {
  const folded = text
    .normalize("NFKD")
    .replace(/[̀-ͯ]/g, "")
    .toLowerCase()
    .replace(/['’]/g, "")
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "");
  if (folded.length <= MAX_SLUG_LENGTH) return folded;
  // Cut at a word boundary when one exists in the second half; never end on a hyphen.
  const cut = folded.slice(0, MAX_SLUG_LENGTH);
  const boundary = cut.lastIndexOf("-");
  return (boundary > MAX_SLUG_LENGTH / 2 ? cut.slice(0, boundary) : cut).replace(/-+$/g, "");
}

/** The canonical slug for a recipe: "37783-pure-cosmic-tool-compatible-gel", or just the id when the name yields nothing. */
export function recipeSlug(recipeId: number, itemName: string | null | undefined): string {
  const name = itemName ? slugify(itemName) : "";
  return name ? `${recipeId}-${name}` : String(recipeId);
}

export interface ParsedRecipeSlug {
  recipeId: number;
  /** The name part as written in the URL; null for a bare id. */
  nameSlug: string | null;
}

/**
 * Reads a recipe URL segment. Only the exact shape "{id}" or "{id}-{lowercase slug}" parses;
 * anything else is not a recipe URL and the page answers 404 without touching the data.
 */
export function parseRecipeSlug(segment: string): ParsedRecipeSlug | null {
  const match = RECIPE_SLUG.exec(segment);
  if (!match) return null;
  const recipeId = Number(match[1]);
  if (!Number.isSafeInteger(recipeId) || recipeId < 1) return null;
  return { recipeId, nameSlug: match[2] ?? null };
}

/** True when the segment is exactly the canonical slug for the item (no redirect needed). */
export function isCanonicalRecipeSlug(segment: string, recipeId: number, itemName: string): boolean {
  return segment === recipeSlug(recipeId, itemName);
}
