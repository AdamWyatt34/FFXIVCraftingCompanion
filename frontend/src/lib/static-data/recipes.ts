import type { RecipeDto } from "@/types/api";
import type { RecipeExport } from "./types";

// GET /api/recipes/{id} returns the material summary only when asked for it.
export function toRecipeDto(data: RecipeExport, includeMaterials: boolean): RecipeDto {
  return includeMaterials ? data.recipe : { ...data.recipe, materialSummary: null };
}
