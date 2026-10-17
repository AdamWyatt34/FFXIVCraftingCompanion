import type { CraftingJourneyDto, ItemContextDto } from "@/types/journey";
import type { RecipeExport } from "./types";
import type { CrystalTable } from "./materials";
import { expandMaterialTree } from "./materials";

// GetCraftingJourneyQuery for a signed-out visitor: the recipe (no material summary), its tree
// at quantity 1 and the result item's context. Sufficiency needs the profile and stays null here.
export function composeJourney(data: RecipeExport, context: ItemContextDto, crystals: CrystalTable): CraftingJourneyDto {
  return {
    recipe: { ...data.recipe, materialSummary: null },
    materials: expandMaterialTree(data.materials, crystals, 1),
    sufficiency: null,
    gearUpgrades: [],
    itemContext: context,
  };
}
