/**
 * Loader and typed wrappers for the WASM build of `crates/xiv-craft` (`src/wasm`, produced by
 * `npm run wasm:build`). Every call takes and returns the API DTO shapes, so a result from here is
 * interchangeable with one from `api-client`. Used on the main thread for the simulator and inside
 * the solver worker for the solvers; each context instantiates its own module.
 */

import type { RecipeCraftingParametersDto } from "@/types/api";
import type {
  ActionAvailability,
  ActionInfo,
  CrafterStatsInput,
  CraftingState,
  ExportMacroResult,
  HQIngredientInput,
  MacroOptions,
  SimulationOptions,
  SimulationResult,
  StepResult,
} from "@/types/simulator";
import type { SolverConstraints, SolverResult } from "@/types/solver";

type WasmModule = typeof import("@/wasm/xiv_craft");

/** `SearchProgress` from the crate: what the MCTS search has found so far. */
export interface SearchProgress {
  iterations: number;
  nodesExplored: number;
  elapsedMs: number;
  bestSteps: number;
  bestQualityPercent: number;
  bestHQPercent: number;
  done: boolean;
}

export interface SolverSessionSettings {
  timeBudgetMs: number;
  maxIterations?: number;
  randomSeed?: number;
}

export interface WasmEngine {
  initialState(
    recipe: RecipeCraftingParametersDto,
    stats: CrafterStatsInput,
    startingQuality: number,
  ): CraftingState;
  executeAction(
    state: CraftingState,
    actionId: number,
    stats: CrafterStatsInput,
    recipe: RecipeCraftingParametersDto,
  ): StepResult;
  availableActions(
    state: CraftingState,
    stats: CrafterStatsInput,
    recipe: RecipeCraftingParametersDto,
  ): ActionAvailability[];
  startingQuality(
    recipe: RecipeCraftingParametersDto,
    hqIngredients: HQIngredientInput[],
  ): number;
  /** The simulate endpoint minus its recipe summary, which the caller fills in. */
  simulate(
    recipe: RecipeCraftingParametersDto,
    stats: CrafterStatsInput,
    actionIds: number[],
    options?: SimulationOptions,
    hqIngredients?: HQIngredientInput[],
  ): Omit<SimulationResult, "recipe"> & { recipe: null };
  exportMacro(actionIds: number[], options?: MacroOptions): ExportMacroResult;
  actionCatalog(level?: number): ActionInfo[];
  /** The greedy solver; no constraints means the API's quick-solve defaults. */
  greedySolve(
    recipe: RecipeCraftingParametersDto,
    stats: CrafterStatsInput,
    startingQuality: number,
    constraints?: SolverConstraints,
  ): SolverResult;
  /**
   * `earlyTermination` is the crate's iterations-without-improvement stop; omitted, it is the
   * C# default of 5000, which at WASM iteration rates ends a search within tens of milliseconds.
   */
  createSolverSession(
    maxNodes: number,
    earlyTermination?: number,
  ): SolverSession;
}

/** A resumable MCTS search; see `SolverSession` in the crate. */
export interface SolverSession {
  begin(
    recipe: RecipeCraftingParametersDto,
    stats: CrafterStatsInput,
    startingQuality: number,
    constraints: SolverConstraints,
    settings: SolverSessionSettings,
  ): void;
  run(maxIterations: number): SearchProgress;
  progress(): SearchProgress;
  finish(): SolverResult;
  treeMemoryBytes(): number;
  free(): void;
}

let enginePromise: Promise<WasmEngine> | null = null;
let loadedAtMs: number | null = null;
let instantiateMs: number | null = null;

/** Milliseconds spent downloading and instantiating the module, once it has loaded. */
export function wasmLoadStats(): {
  loadedAtMs: number;
  instantiateMs: number;
} | null {
  return loadedAtMs !== null && instantiateMs !== null
    ? { loadedAtMs, instantiateMs }
    : null;
}

function wrap(mod: WasmModule): WasmEngine {
  return {
    initialState: (recipe, stats, startingQuality) =>
      mod.initial_state(recipe, stats, startingQuality) as CraftingState,
    executeAction: (state, actionId, stats, recipe) =>
      mod.execute_action(state, actionId, stats, recipe) as StepResult,
    availableActions: (state, stats, recipe) =>
      mod.available_actions(state, stats, recipe) as ActionAvailability[],
    startingQuality: (recipe, hqIngredients) =>
      mod.starting_quality(recipe, hqIngredients),
    simulate: (recipe, stats, actionIds, options, hqIngredients) =>
      mod.simulate(
        recipe,
        stats,
        Int32Array.from(actionIds),
        options ?? null,
        hqIngredients ?? null,
      ) as Omit<SimulationResult, "recipe"> & { recipe: null },
    exportMacro: (actionIds, options) =>
      mod.export_macro(
        Int32Array.from(actionIds),
        options ?? null,
      ) as ExportMacroResult,
    actionCatalog: (level) => mod.action_catalog(level ?? null) as ActionInfo[],
    greedySolve: (recipe, stats, startingQuality, constraints) =>
      mod.greedy_solve(
        recipe,
        stats,
        startingQuality,
        constraints ?? null,
      ) as SolverResult,
    createSolverSession: (maxNodes, earlyTermination) => {
      const session = new mod.SolverSession(maxNodes, earlyTermination ?? null);
      return {
        begin: (recipe, stats, startingQuality, constraints, settings) =>
          session.begin(recipe, stats, startingQuality, constraints, settings),
        run: (maxIterations) => session.run(maxIterations) as SearchProgress,
        progress: () => session.progress() as SearchProgress,
        finish: () => session.finish() as SolverResult,
        treeMemoryBytes: () => session.tree_memory_bytes(),
        free: () => session.free(),
      };
    },
  };
}

/**
 * Loads and instantiates the module once per JS context. The glue resolves `xiv_craft_bg.wasm`
 * relative to itself with `new URL(..., import.meta.url)`, which webpack emits as a hashed static
 * asset and the service worker precaches.
 */
export function loadWasmEngine(): Promise<WasmEngine> {
  if (!enginePromise) {
    const started = performance.now();
    enginePromise = import("@/wasm/xiv_craft")
      .then(async (mod) => {
        await mod.default();
        instantiateMs = performance.now() - started;
        loadedAtMs = performance.now();
        return wrap(mod);
      })
      .catch((err) => {
        enginePromise = null;
        throw err;
      });
  }
  return enginePromise;
}
