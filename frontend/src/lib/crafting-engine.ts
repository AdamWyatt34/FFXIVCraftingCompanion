/**
 * The simulator's crafting calls, with the signatures and result shapes the .NET crafting
 * endpoints had. Every call runs in the browser through the WASM engine; recipe parameters come
 * from the static game data and are memoized here per recipe id.
 */

import { ApiClientError } from "@/lib/api-client";
import { getRecipe, getRecipeCraftingParameters } from "@/lib/game-data";
import { loadWasmEngine } from "@/lib/wasm-engine";
import type {
  RecipeCraftingParametersDto,
  RecipeSummaryDto,
} from "@/types/api";
import type {
  ActionAvailability,
  ActionInfo,
  AvailableActionsRequest,
  CrafterStatsInput,
  CraftingState,
  ExecuteActionRequest,
  ExportMacroRequest,
  ExportMacroResult,
  SimulateRequest,
  SimulationResult,
  StepResult,
} from "@/types/simulator";

const parametersCache = new Map<number, Promise<RecipeCraftingParametersDto>>();

/** The engine parameters for a recipe, fetched once per page load and shared by every call. */
export function getRecipeParameters(
  recipeId: number,
  signal?: AbortSignal,
): Promise<RecipeCraftingParametersDto> {
  const cached = parametersCache.get(recipeId);
  if (cached) return cached;
  const promise = getRecipeCraftingParameters(recipeId, signal).catch((err) => {
    parametersCache.delete(recipeId);
    throw err;
  });
  parametersCache.set(recipeId, promise);
  return promise;
}

/**
 * Warms the parameter cache (memory and, through the service worker, disk) as soon as a recipe is
 * selected, so a recipe opened online can be crafted and solved offline even if no engine call
 * was made while online.
 */
export function prefetchRecipeParameters(recipeId: number): void {
  getRecipeParameters(recipeId).catch(() => {
    // The next engine call reports the failure with the API's message.
  });
}

function throwIfAborted(signal?: AbortSignal): void {
  if (signal?.aborted) throw new DOMException("Aborted", "AbortError");
}

/** The API answers bad input with 400 and a message; the local path raises the same shape. */
function badRequest(message: string): ApiClientError {
  return new ApiClientError(400, { error: message });
}

function validateLevel(level: number): void {
  if (level < 1 || level > 100)
    throw badRequest("Level must be between 1 and 100.");
}

/** WASM raises plain errors carrying the API's messages; surface them as the API would. */
function local<T>(compute: () => T): T {
  try {
    return compute();
  } catch (err) {
    if (err instanceof ApiClientError) throw err;
    throw badRequest(err instanceof Error ? err.message : String(err));
  }
}

async function recipeSummary(
  recipeId: number,
  signal?: AbortSignal,
): Promise<RecipeSummaryDto> {
  throwIfAborted(signal);
  const recipe = await getRecipe(recipeId);
  return {
    recipeId: recipe.recipeId,
    craftType: recipe.craftType,
    craftTypeName: recipe.craftType,
    recipeLevel: recipe.recipeLevel,
    requiredCraftsmanship: recipe.requiredCraftsmanship,
    requiredControl: recipe.requiredControl,
    yield: recipe.yield,
  };
}

export async function getInitialCraftingState(
  recipeId: number,
  level: number,
  craftsmanship: number,
  control: number,
  cp: number,
  isSpecialist = false,
  signal?: AbortSignal,
  startingQuality = 0,
): Promise<CraftingState> {
  validateLevel(level);
  if (craftsmanship < 0 || control < 0 || cp < 0)
    throw badRequest("Stats must be non-negative.");
  if (startingQuality < 0)
    throw badRequest("Starting quality must be non-negative.");

  const [engine, recipe] = await Promise.all([
    loadWasmEngine(),
    getRecipeParameters(recipeId, signal),
  ]);
  throwIfAborted(signal);
  const stats: CrafterStatsInput = {
    level,
    craftsmanship,
    control,
    cp,
    isSpecialist,
  };
  return local(() => engine.initialState(recipe, stats, startingQuality));
}

export async function executeCraftingAction(
  request: ExecuteActionRequest,
  signal?: AbortSignal,
): Promise<StepResult> {
  validateLevel(request.stats.level);

  const [engine, recipe] = await Promise.all([
    loadWasmEngine(),
    getRecipeParameters(request.recipeId, signal),
  ]);
  throwIfAborted(signal);
  return local(() =>
    engine.executeAction(
      request.state,
      request.actionId,
      request.stats,
      recipe,
    ),
  );
}

export async function getAvailableActions(
  request: AvailableActionsRequest,
  signal?: AbortSignal,
): Promise<ActionAvailability[]> {
  validateLevel(request.stats.level);

  const [engine, recipe] = await Promise.all([
    loadWasmEngine(),
    getRecipeParameters(request.recipeId, signal),
  ]);
  throwIfAborted(signal);
  return local(() =>
    engine.availableActions(request.state, request.stats, recipe),
  );
}

export async function simulateCraft(
  request: SimulateRequest,
  signal?: AbortSignal,
): Promise<SimulationResult> {
  if (!request.stats) throw badRequest("Stats are required.");
  const stats = request.stats;
  validateLevel(stats.level);
  if (stats.craftsmanship < 0 || stats.control < 0 || stats.cp < 0) {
    throw badRequest("Stats must be non-negative.");
  }

  const [engine, recipe, summary] = await Promise.all([
    loadWasmEngine(),
    getRecipeParameters(request.recipeId, signal),
    recipeSummary(request.recipeId, signal),
  ]);
  throwIfAborted(signal);
  const result = local(() =>
    engine.simulate(
      recipe,
      stats,
      request.actionIds,
      request.options,
      request.hqIngredients,
    ),
  );
  return { ...result, recipe: summary };
}

export async function exportCraftingMacro(
  request: ExportMacroRequest,
  signal?: AbortSignal,
): Promise<ExportMacroResult> {
  const engine = await loadWasmEngine();
  throwIfAborted(signal);
  return local(() => engine.exportMacro(request.actionIds, request.options));
}

export async function getCraftingActions(
  level?: number,
  signal?: AbortSignal,
): Promise<ActionInfo[]> {
  const engine = await loadWasmEngine();
  throwIfAborted(signal);
  return local(() => engine.actionCatalog(level));
}
