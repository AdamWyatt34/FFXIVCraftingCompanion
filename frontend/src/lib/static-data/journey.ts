import type { CraftingJourneyDto, ItemContextDto, StatSufficiencyResultDto } from "@/types/journey";
import type { RecipeExport } from "./types";
import type { CrystalTable } from "./materials";
import { expandMaterialTree } from "./materials";

// GetCraftingJourneyQuery: the recipe (no material summary), its tree at quantity 1, the result
// item's context and, for a signed-in visitor, the stat check (see sufficiency.ts).
export function composeJourney(
  data: RecipeExport,
  context: ItemContextDto,
  crystals: CrystalTable,
  sufficiency: StatSufficiencyResultDto | null = null,
): CraftingJourneyDto {
  return {
    recipe: { ...data.recipe, materialSummary: null },
    materials: expandMaterialTree(data.materials, crystals, 1),
    sufficiency,
    gearUpgrades: sufficiency?.suggestedGearUpgrades ?? [],
    itemContext: context,
  };
}
