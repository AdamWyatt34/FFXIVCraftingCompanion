import { recipeSlug } from "@/lib/static-data/slug";
import type { QuickFilter } from "@/lib/static-data/search";
import type { CraftType } from "@/types/api";

// Every internal URL the app links to is built here, so a URL scheme change is one edit. Recipe
// URLs are /recipes/{id}-{item-name}: callers pass the item name whenever they have it, and a bare
// id still works (the page answers a 308 to the slug), so nothing breaks where no name is at hand.

export function recipePath(recipeId: number, itemName?: string | null): string {
  return `/recipes/${recipeSlug(recipeId, itemName)}`;
}

export function recipeJourneyPath(recipeId: number, itemName?: string | null): string {
  return `${recipePath(recipeId, itemName)}/journey`;
}

/** The job hub, or one of its level-range pages: /recipes/by-job/crp/91-100. */
export function recipesByJobPath(craftType: CraftType, bracketSlug?: string): string {
  const base = `/recipes/by-job/${craftType.toLowerCase()}`;
  return bracketSlug ? `${base}/${bracketSlug}` : base;
}

export const DAWNTRAIL_RECIPES_PATH = "/recipes/dawntrail";

/** The recipe browser with a filter applied (a client-side filter, canonicalised to /recipes). */
export function recipeFilterPath(filter: { craftType?: CraftType; quickFilter?: QuickFilter }): string {
  const params = new URLSearchParams();
  if (filter.craftType) params.set("craftType", filter.craftType);
  if (filter.quickFilter) params.set("quickFilter", filter.quickFilter);
  const query = params.toString();
  return query ? `/recipes?${query}` : "/recipes";
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
