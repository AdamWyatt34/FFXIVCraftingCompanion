import type { RecipeCraftingParametersDto, RecipeDto } from "@/types/api";
import type { RecipeExport } from "./types";

// GET /api/recipes/{id} returns the material summary only when asked for it.
export function toRecipeDto(data: RecipeExport, includeMaterials: boolean): RecipeDto {
  return includeMaterials ? data.recipe : { ...data.recipe, materialSummary: null };
}

/** The crafting parameters with the recipe's required stats always present. */
export type CraftingParameters = RecipeCraftingParametersDto & {
  requiredCraftsmanship: number;
  requiredControl: number;
};

// A data set exported without the Recipe sheet's required stats carries none here, and its
// recipe.required* holds the recommended value, which then stays the gate.
export function withRequiredStats(
  parameters: RecipeCraftingParametersDto,
  recipe: Pick<RecipeDto, "requiredCraftsmanship" | "requiredControl">,
): CraftingParameters {
  return {
    ...parameters,
    requiredCraftsmanship: parameters.requiredCraftsmanship ?? recipe.requiredCraftsmanship,
    requiredControl: parameters.requiredControl ?? recipe.requiredControl,
  };
}
