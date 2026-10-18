import { recipeSlug } from "@/lib/static-data/slug";

// Every internal URL the app links to is built here, so a URL scheme change is one edit. Recipe
// URLs are /recipes/{id}-{item-name}: callers pass the item name whenever they have it, and a bare
// id still works (the page answers a 308 to the slug), so nothing breaks where no name is at hand.

export function recipePath(recipeId: number, itemName?: string | null): string {
  return `/recipes/${recipeSlug(recipeId, itemName)}`;
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
