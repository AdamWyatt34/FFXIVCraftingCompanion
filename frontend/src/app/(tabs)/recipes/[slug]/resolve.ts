import { cache } from "react";
import { getRecipeExportServer } from "@/lib/static-data/server";
import { parseRecipeSlug, recipeSlug } from "@/lib/static-data/slug";
import type { RecipeExport } from "@/lib/static-data/types";

export type RecipeRoute =
  /** Not a recipe URL, or no such recipe: a real 404. */
  | { status: "not-found" }
  /** A bare id or a stale name: the page answers a 308 to the canonical slug. */
  | { status: "redirect"; recipeId: number; itemName: string }
  | { status: "ok"; recipeId: number; itemName: string; data: RecipeExport };

/**
 * Resolves the /recipes/{slug} segment. The shape is validated before any data is read (a junk
 * URL would otherwise cost a render and a cache write each), the recipe is read once per request
 * and shared by generateMetadata and the page, and the result says whether the segment is the
 * canonical slug. The recipe is resolved before any Suspense boundary and there is no loading.tsx
 * in this segment: a streamed shell would fix the status at 200 and turn the 404 and the 308 into
 * soft 200s.
 */
export const resolveRecipeRoute = cache(async (segment: string): Promise<RecipeRoute> => {
  const parsed = parseRecipeSlug(segment);
  if (!parsed) return { status: "not-found" };
  const data = await getRecipeExportServer(parsed.recipeId);
  if (!data) return { status: "not-found" };
  const recipeId = data.recipe.recipeId;
  const itemName = data.recipe.resultItem.name;
  return segment === recipeSlug(recipeId, itemName)
    ? { status: "ok", recipeId, itemName, data }
    : { status: "redirect", recipeId, itemName };
});
