import { ApiClientError, getSharedCraftPlan } from "@/lib/api-client";
import * as staticData from "@/lib/static-data/client";
import type {
  ItemDto,
  ItemSearchResultDto,
  RecipeDto,
  RecipeCraftingParametersDto,
  MaterialTreeResultDto,
} from "@/types/api";
import type { GearSetDto, StatWeightProfileDto, EquipmentDto } from "@/types/gear";

// Game data is read from the exported static files (src/lib/static-data), not the API. The
// signatures and the DTOs are the API's, so callers do not care which one answered. This module
// is separate from api-client so the account code (and the root layout, through AuthContext)
// does not carry the static-data port in its chunk; only the pages that read game data load it.

export function searchItems(
  query: string | null,
  page = 1,
  pageSize = 20,
  signal?: AbortSignal,
  craftType?: string,
  quickFilter?: import("@/lib/static-data/search").QuickFilter | null,
): Promise<ItemSearchResultDto> {
  return staticData.searchItems(query, page, pageSize, signal, craftType, quickFilter);
}

export function getItem(id: number): Promise<ItemDto> {
  return staticData.getItem(id);
}

export function getRecipe(
  id: number,
  includeMaterials = false,
): Promise<RecipeDto> {
  return staticData.getRecipe(id, includeMaterials);
}

export function getRecipeCraftingParameters(
  id: number,
  signal?: AbortSignal,
): Promise<RecipeCraftingParametersDto> {
  return staticData.getRecipeCraftingParameters(id, signal);
}

export function getMaterialTree(
  recipeId: number,
  quantity = 1,
  signal?: AbortSignal,
): Promise<MaterialTreeResultDto> {
  return staticData.getMaterialTree(recipeId, quantity, signal);
}

export function getGearRecommendations(
  job: string,
  level: number,
  profile: string,
  signal?: AbortSignal,
  craftedOnly?: boolean,
): Promise<GearSetDto> {
  return staticData.getGearRecommendations(job, level, profile, signal, craftedOnly ?? false);
}

export function getStatWeightProfiles(
  job: string,
  signal?: AbortSignal,
): Promise<StatWeightProfileDto[]> {
  return staticData.getStatWeightProfiles(job, signal);
}

export function searchEquipment(
  query: string,
  job?: string,
  slot?: string,
  minLevel?: number,
  maxLevel?: number,
  signal?: AbortSignal,
): Promise<EquipmentDto[]> {
  return staticData.searchEquipment(query, job, slot, minLevel, maxLevel, signal);
}

/** The journey; with a viewer (a signed-in profile and the engine) it carries the stat check. */
export function getCraftingJourney(
  recipeId: number,
  signal?: AbortSignal,
  viewer: import("@/lib/static-data/sufficiency").JourneyViewer | null = null,
): Promise<import("@/types/journey").CraftingJourneyDto> {
  return staticData.getCraftingJourney(recipeId, signal, viewer);
}

export function getItemContext(
  itemId: number,
  signal?: AbortSignal,
): Promise<import("@/types/journey").ItemContextDto> {
  return staticData.getItemContext(itemId, signal);
}

export function getWorldList(): Promise<import("@/types/market").WorldGroupDto[]> {
  return staticData.getWorldList();
}

export function getRecipeCost(
  recipeId: number,
  world: string,
  quantity = 1,
  options?: { useMinPrice?: boolean; includeGatheredItemPrices?: boolean },
  signal?: AbortSignal,
): Promise<import("@/types/market").CostEstimateDto> {
  return staticData.getRecipeCost(recipeId, world, quantity, options ?? {}, signal);
}

export function getGatheringGuide(
  items: import("@/types/gathering").GatheringGuideItemInput[],
  signal?: AbortSignal,
): Promise<import("@/types/gathering").GatheringGuideDto> {
  return staticData.getGatheringGuide(items, signal);
}

export function getEorzeanTime(): Promise<import("@/types/gathering").EorzeanTimeResponse> {
  return staticData.getEorzeanTime();
}

export function resolveCraftPlan(
  items: import("@/types/craft-plan").CraftPlanItemInput[],
  signal?: AbortSignal,
): Promise<import("@/types/craft-plan").CraftPlanResultDto> {
  return staticData.resolveCraftPlan(items, signal);
}

export function exportCraftPlan(
  items: import("@/types/craft-plan").CraftPlanItemInput[],
  format: string,
  signal?: AbortSignal,
): Promise<import("@/types/craft-plan").ExportResultDto> {
  return staticData.exportCraftPlan(items, format, signal);
}

export async function getResolvedCraftPlan(
  id: string,
  signal?: AbortSignal,
): Promise<import("@/types/craft-plan").CraftPlanResultDto> {
  const plan = await getSharedCraftPlan(id, signal);
  if (plan.items.length === 0)
    throw new ApiClientError(404, { error: "Craft plan not found." });
  return staticData.resolveCraftPlan(plan.items, signal);
}

export async function getBisStats(
  job: string,
  level: number,
  signal?: AbortSignal,
): Promise<{ craftsmanship: number; control: number; cp: number }> {
  const gearSet = await getGearRecommendations(
    job,
    level,
    "balanced",
    signal,
    false,
  );
  return {
    craftsmanship: gearSet.totalStats.craftsmanship,
    control: gearSet.totalStats.control,
    cp: gearSet.totalStats.cp,
  };
}
