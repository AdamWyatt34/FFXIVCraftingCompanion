import { recipeSlug } from "@/lib/static-data/slug";

// Every internal URL the app links to is built here, so a URL scheme change is one edit. Recipe
// links take the item name whenever the caller has it; while USE_RECIPE_SLUGS is off they stay
// numeric and the name is ignored, and turning slugs on changes nothing at the call sites.

const USE_RECIPE_SLUGS = false;

export function recipePath(recipeId: number, itemName?: string | null): string {
  return USE_RECIPE_SLUGS && itemName
    ? `/recipes/${recipeSlug(recipeId, itemName)}`
    : `/recipes/${recipeId}`;
}

export function recipeJourneyPath(recipeId: number, itemName?: string | null): string {
  return `${recipePath(recipeId, itemName)}/journey`;
}

export function itemPath(itemId: number): string {
  return `/items/${itemId}`;
}

export function rotationPath(rotationId: number): string {
  return `/simulator/library/${rotationId}`;
}

/** The simulator with a recipe preselected; `solve` opens it in solver mode. */
export function simulatorPath(recipeId: number, options?: { solve?: boolean }): string {
  return options?.solve ? `/simulator?recipeId=${recipeId}&solve=true` : `/simulator?recipeId=${recipeId}`;
}
