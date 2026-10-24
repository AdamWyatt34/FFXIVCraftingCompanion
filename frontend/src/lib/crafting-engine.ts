/**
 * The simulator's crafting calls, with the signatures and result shapes the .NET crafting
 * endpoints had. Every call runs in the browser through the WASM engine; recipe parameters come
 * from the static game data and are memoized here per recipe id.
 */

import { ApiClientError } from "@/lib/api-client";
import { getRecipe, getRecipeCraftingParameters } from "@/lib/game-data";
import { startingQualityProblem } from "@/lib/simulator-setup";
import {
  isFatalEngineError,
  loadWasmEngine,
  resetWasmEngine,
  type WasmEngine,
} from "@/lib/wasm-engine";
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

function validateStartingQuality(startingQuality: number | undefined): void {
  const problem = startingQualityProblem(startingQuality);
  if (problem) throw badRequest(problem);
}

/**
 * WASM raises plain errors carrying the API's messages; surface them as the API would. A trap
 * leaves the instance unusable, so it is forgotten and the next call instantiates a fresh one.
 */
function local<T>(compute: () => T): T {
  try {
    return compute();
  } catch (err) {
    if (err instanceof ApiClientError) throw err;
    if (isFatalEngineError(err)) {
      resetWasmEngine();
      throw new ApiClientError(500, {
        error: "The crafting engine hit an internal error and was reloaded. Please try again.",
      });
    }
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
  validateStartingQuality(startingQuality);

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

/** `startingQuality` is the quality the craft starts from, in place of what `hqIngredients` give. */
export interface SimulateCraftRequest extends SimulateRequest {
  startingQuality?: number;
}

export async function simulateCraft(
  request: SimulateCraftRequest,
  signal?: AbortSignal,
): Promise<SimulationResult> {
  if (!request.stats) throw badRequest("Stats are required.");
  const stats = request.stats;
  validateLevel(stats.level);
  if (stats.craftsmanship < 0 || stats.control < 0 || stats.cp < 0) {
    throw badRequest("Stats must be non-negative.");
  }
  validateStartingQuality(request.startingQuality);
  const startingQuality = request.startingQuality ?? 0;
  if (startingQuality > 0 && (request.options || request.hqIngredients?.length))
    throw badRequest(
      "A starting quality replaces the HQ ingredients and runs with the default options.",
    );

  const [engine, recipe, summary] = await Promise.all([
    loadWasmEngine(),
    getRecipeParameters(request.recipeId, signal),
    recipeSummary(request.recipeId, signal),
  ]);
  throwIfAborted(signal);
  const result = local(() =>
    startingQuality > 0
      ? simulateFrom(engine, recipe, stats, request.actionIds, startingQuality)
      : engine.simulate(
          recipe,
          stats,
          request.actionIds,
          request.options,
          request.hqIngredients,
        ),
  );
  return { ...result, recipe: summary };
}

// The engine's simulate takes no starting quality; under the default options (every action
// succeeds, the condition stays Normal) stepping from an initial state that has one is the same run.
function simulateFrom(
  engine: WasmEngine,
  recipe: RecipeCraftingParametersDto,
  stats: CrafterStatsInput,
  actionIds: number[],
  startingQuality: number,
): Omit<SimulationResult, "recipe"> {
  if (actionIds.length === 0)
    throw badRequest(
      "At least one action is required (provide ActionIds or ActionNames).",
    );
  let state = engine.initialState(recipe, stats, startingQuality);
  const steps: StepResult[] = [];
  for (const actionId of actionIds) {
    if (state.status !== "InProgress") break;
    const step = engine.executeAction(state, actionId, stats, recipe);
    steps.push(step);
    state = step.stateAfter;
  }
  return {
    success: state.status === "Completed",
    totalSteps: steps.length,
    finalQualityPercent: state.qualityPercent,
    finalHQPercent: state.hqPercent,
    finalState: state,
    steps,
    macros: engine.exportMacro(actionIds).macros,
  };
}

export async function exportCraftingMacro(
  request: ExportMacroRequest,
  signal?: AbortSignal,
): Promise<ExportMacroResult> {
  const engine = await loadWasmEngine();
  throwIfAborted(signal);
  return local(() => engine.exportMacro(request.actionIds, request.options));
}

/** The palette: the actions the game offers, filtered to `level` when given. */
export async function getCraftingActions(
  level?: number,
  signal?: AbortSignal,
): Promise<ActionInfo[]> {
  const engine = await loadWasmEngine();
  throwIfAborted(signal);
  return local(() => engine.actionCatalog(level).filter((a) => !a.isLegacy));
}

/** Every action id the engine replays, legacy ones included, at any level; saved and shared rotations are checked against these. */
export async function getRecognizedActionIds(
  signal?: AbortSignal,
): Promise<Set<number>> {
  const engine = await loadWasmEngine();
  throwIfAborted(signal);
  return local(() => new Set(engine.actionCatalog().map((a) => a.id)));
}
