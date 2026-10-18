import { ApiClientError } from "@/lib/api-errors";
import type {
  ItemDto,
  ItemSearchResultDto,
  MaterialTreeResultDto,
  RecipeCraftingParametersDto,
  RecipeDto,
} from "@/types/api";
import type { CraftPlanItemInput, CraftPlanResultDto, ExportResultDto } from "@/types/craft-plan";
import type { CraftingJourneyDto, ItemContextDto, StatSufficiencyResultDto } from "@/types/journey";
import type { EquipmentDto, GearSetDto, StatWeightProfileDto } from "@/types/gear";
import type { EorzeanTimeResponse, GatheringGuideDto, GatheringGuideItemInput } from "@/types/gathering";
import type { CostEstimateDto, WorldGroupDto } from "@/types/market";
import { CRYSTALS_PATH, GEAR_PROFILES_PATH, gearJobPath, itemBucketPath, recipeBucketPath } from "./paths";
import type { QuickFilter } from "./search";
import { getSearchIndex, searchGearItemIds, searchItemsIndexed } from "./search-client";
import { loadJson } from "./fetch";
import { toRecipeDto } from "./recipes";
import { expandMaterialTree, type CrystalTable } from "./materials";
import { aggregateCraftPlan, exportCraftPlanContent, validatePlanItems, type ExportFormat, type PlanEntry } from "./craft-plans";
import { composeJourney } from "./journey";
import { analyzeSufficiency, type GearSource, type JourneyViewer } from "./sufficiency";
import { availableProfiles, isEquipSlot, recommendGear, toSearchResultDto } from "./gear";
import { ALL_JOBS, isKnownJob } from "./jobs";
import { buildGatheringGuide, validateGuideItems, type GatheringItemData } from "./gathering";
import { estimateCost, fetchUniversalisPrices, isValidWorldOrDc, marketBoardItemIds, worldGroups, type CostEstimationOptions } from "./market";
import { getCurrentEorzeanHour } from "@/lib/eorzean-time";
import type { Bucket, GearJobExport, ItemExport, RecipeExport, StatWeightProfileExport } from "./types";

// Browser-side reads of the exported game data; the fetch and its per-load cache live in ./fetch.
export { loadJson };

function notFound(what: string): ApiClientError {
  return new ApiClientError(404, { error: `${what} not found` });
}

async function fromBucket<T>(path: string, id: number, what: string, signal?: AbortSignal): Promise<T> {
  let bucket: Bucket<T>;
  try {
    bucket = await loadJson<Bucket<T>>(path, signal);
  } catch (err) {
    if (err instanceof ApiClientError && err.status === 404) throw notFound(what);
    throw err;
  }
  const entry = bucket[String(id)];
  if (!entry) throw notFound(what);
  return entry;
}

export function getRecipeExport(recipeId: number, signal?: AbortSignal): Promise<RecipeExport> {
  return fromBucket<RecipeExport>(recipeBucketPath(recipeId), recipeId, "Recipe", signal);
}

export function getItemExport(itemId: number, signal?: AbortSignal): Promise<ItemExport> {
  return fromBucket<ItemExport>(itemBucketPath(itemId), itemId, "Item", signal);
}

export async function getRecipe(recipeId: number, includeMaterials = false): Promise<RecipeDto> {
  return toRecipeDto(await getRecipeExport(recipeId), includeMaterials);
}

export async function getRecipeCraftingParameters(
  recipeId: number,
  signal?: AbortSignal,
): Promise<RecipeCraftingParametersDto> {
  return (await getRecipeExport(recipeId, signal)).craftingParameters;
}

export async function getItem(itemId: number): Promise<ItemDto> {
  return (await getItemExport(itemId)).item;
}

export async function getItemContext(itemId: number, signal?: AbortSignal): Promise<ItemContextDto> {
  return (await getItemExport(itemId, signal)).context;
}

export function getCrystals(signal?: AbortSignal): Promise<CrystalTable> {
  return loadJson<CrystalTable>(CRYSTALS_PATH, signal);
}

export async function getMaterialTree(
  recipeId: number,
  quantity = 1,
  signal?: AbortSignal,
): Promise<MaterialTreeResultDto> {
  if (quantity < 1 || quantity > 999)
    throw new ApiClientError(400, { error: "Quantity must be between 1 and 999." });
  const [data, crystals] = await Promise.all([getRecipeExport(recipeId, signal), getCrystals(signal)]);
  return expandMaterialTree(data.materials, crystals, quantity);
}

async function planEntries(items: CraftPlanItemInput[], signal?: AbortSignal): Promise<PlanEntry[]> {
  const error = validatePlanItems(items);
  if (error) throw new ApiClientError(400, { error });
  const crystals = await getCrystals(signal);
  const entries: PlanEntry[] = [];
  for (const input of items) {
    let data: RecipeExport;
    try {
      data = await getRecipeExport(input.recipeId, signal);
    } catch (err) {
      if (err instanceof ApiClientError && err.status === 404)
        throw new ApiClientError(404, { error: "One or more recipes not found." });
      throw err;
    }
    entries.push({ input, recipe: data.recipe, tree: expandMaterialTree(data.materials, crystals, input.quantity) });
  }
  return entries;
}

export async function resolveCraftPlan(items: CraftPlanItemInput[], signal?: AbortSignal): Promise<CraftPlanResultDto> {
  return aggregateCraftPlan(await planEntries(items, signal));
}

export async function exportCraftPlan(
  items: CraftPlanItemInput[],
  format: string,
  signal?: AbortSignal,
): Promise<ExportResultDto> {
  const normalized = format.toLowerCase();
  if (normalized !== "text" && normalized !== "json" && normalized !== "teamcraft")
    throw new ApiClientError(400, { error: "Format must be one of: text, json, teamcraft." });
  const result = aggregateCraftPlan(await planEntries(items, signal));
  return { format: normalized, content: exportCraftPlanContent(result, normalized as ExportFormat) };
}

export async function getCraftingJourney(
  recipeId: number,
  signal?: AbortSignal,
  viewer: JourneyViewer | null = null,
): Promise<CraftingJourneyDto> {
  const [data, crystals] = await Promise.all([getRecipeExport(recipeId, signal), getCrystals(signal)]);
  const context = await getItemContext(data.recipe.resultItem.itemId, signal);
  const sufficiency = viewer ? await resolveSufficiency(data, viewer, signal) : null;
  return composeJourney(data, context, crystals, sufficiency);
}

// GetCraftingJourneyQuery.ResolveSufficiencyAsync: a failed stat check leaves the journey without one.
async function resolveSufficiency(
  data: RecipeExport,
  viewer: JourneyViewer,
  signal?: AbortSignal,
): Promise<StatSufficiencyResultDto | null> {
  try {
    return await analyzeSufficiency(data.recipe, data.craftingParameters, viewer.stats, viewer.profile, viewer.engine, gearSource(signal));
  } catch (err) {
    if (err instanceof DOMException && err.name === "AbortError") throw err;
    console.warn(`Sufficiency analysis failed for recipe ${data.recipe.recipeId}`, err);
    return null;
  }
}

function gearSource(signal?: AbortSignal): GearSource {
  return {
    async recommend(job, level) {
      const [jobFile, profiles] = await Promise.all([getGearJob(job, signal), getGearProfilesTable(signal)]);
      return recommendGear(jobFile, profiles, level, "balanced", false);
    },
    async equipment(ids) {
      const found = await Promise.all(
        ids.map(async (id) => {
          try {
            return (await getItemExport(id, signal)).equipment;
          } catch (err) {
            if (err instanceof ApiClientError && err.status === 404) return null;
            throw err;
          }
        }),
      );
      return new Map(found.filter((e): e is EquipmentDto => e !== null).map((e) => [e.equipmentId, e]));
    },
  };
}

export function getGearProfilesTable(signal?: AbortSignal): Promise<StatWeightProfileExport[]> {
  return loadJson<StatWeightProfileExport[]>(GEAR_PROFILES_PATH, signal);
}

export function getGearJob(job: string, signal?: AbortSignal): Promise<GearJobExport> {
  return loadJson<GearJobExport>(gearJobPath(job), signal);
}

function badRequest(error: string): ApiClientError {
  return new ApiClientError(400, { error });
}

export async function getGearRecommendations(
  job: string,
  level: number,
  profile: string,
  signal?: AbortSignal,
  craftedOnly = false,
): Promise<GearSetDto> {
  if (!job || job.trim() === "") throw badRequest("Job is required.");
  const normalized = job.toUpperCase();
  if (!isKnownJob(normalized))
    throw badRequest(`'${job}' is not a valid job. Valid jobs: ${ALL_JOBS.join(", ")}.`);
  if (level < 1 || level > 100) throw badRequest("Level must be between 1 and 100.");
  const [jobFile, profiles] = await Promise.all([getGearJob(normalized, signal), getGearProfilesTable(signal)]);
  return recommendGear(jobFile, profiles, level, profile.toLowerCase(), craftedOnly);
}

export async function getStatWeightProfiles(job?: string | null, signal?: AbortSignal): Promise<StatWeightProfileDto[]> {
  if (job && job.trim() !== "" && !isKnownJob(job)) throw badRequest(`'${job}' is not a valid job.`);
  const profiles = await getGearProfilesTable(signal);
  return availableProfiles(profiles, job?.toUpperCase() ?? "CRP");
}

export async function searchEquipment(
  query: string,
  job?: string,
  slot?: string,
  minLevel?: number,
  maxLevel?: number,
  signal?: AbortSignal,
): Promise<EquipmentDto[]> {
  if ((query.trim() === "" || query.length < 2) && !job && !slot)
    throw badRequest("Provide a search query (min 2 characters) or a filter.");
  if (slot && !isEquipSlot(slot)) throw badRequest(`'${slot}' is not a valid equipment slot.`);
  const itemIds = await searchGearItemIds({ query, job, slot, minLevel, maxLevel }, signal);
  const exports = await Promise.all(itemIds.map((id) => getItemExport(id, signal)));
  return exports.flatMap((e) => (e.equipment ? [toSearchResultDto(e.equipment)] : []));
}

export async function getGatheringGuide(items: GatheringGuideItemInput[], signal?: AbortSignal): Promise<GatheringGuideDto> {
  const error = validateGuideItems(items);
  if (error) throw badRequest(error);
  const ids = [...new Set(items.map((i) => i.itemId))];
  const exports = new Map<number, GatheringItemData | null>();
  await Promise.all(
    ids.map(async (id) => {
      try {
        const data = await getItemExport(id, signal);
        exports.set(id, { item: data.item, gatheringNodes: data.gatheringNodes });
      } catch (err) {
        if (err instanceof ApiClientError && err.status === 404) exports.set(id, null);
        else throw err;
      }
    }),
  );
  return buildGatheringGuide(items, (id) => exports.get(id) ?? null, getCurrentEorzeanHour());
}

export function getEorzeanTime(): Promise<EorzeanTimeResponse> {
  return Promise.resolve({ currentEorzeanHour: getCurrentEorzeanHour() });
}

export function getWorldList(): Promise<WorldGroupDto[]> {
  return Promise.resolve(worldGroups());
}

export async function getRecipeCost(
  recipeId: number,
  world: string,
  quantity = 1,
  options: CostEstimationOptions = {},
  signal?: AbortSignal,
): Promise<CostEstimateDto> {
  if (world.trim() === "" || !isValidWorldOrDc(world)) throw badRequest("Invalid or missing world/data center name.");
  if (quantity < 1 || quantity > 999) throw badRequest("Quantity must be between 1 and 999.");
  const [data, crystals] = await Promise.all([getRecipeExport(recipeId, signal), getCrystals(signal)]);
  const tree = expandMaterialTree(data.materials, crystals, quantity);
  const materials = [...tree.shoppingList, ...tree.crystalRequirements];
  const finalItemId = (options.includeCraftVsBuyAnalysis ?? true) ? data.recipe.resultItem.itemId : null;

  const warnings: string[] = [];
  let prices = new Map();
  try {
    prices = await fetchUniversalisPrices(world, marketBoardItemIds(materials, finalItemId, options), signal);
  } catch (err) {
    if (err instanceof DOMException && err.name === "AbortError") throw err;
    warnings.push(`Could not fetch some market board prices from Universalis: ${err instanceof Error ? err.message : String(err)}`);
  }
  return estimateCost(materials, finalItemId, world, options, prices, Date.now(), warnings);
}

export { getSearchIndex };

export function searchItems(
  query: string | null,
  page = 1,
  pageSize = 20,
  signal?: AbortSignal,
  craftType?: string,
  quickFilter?: QuickFilter | null,
): Promise<ItemSearchResultDto> {
  return searchItemsIndexed({ query, page, pageSize, craftType: craftType ?? null, quickFilter: quickFilter ?? null }, signal);
}
