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
import type { SearchProgress, SolverSessionSettings } from "@/wasm/xiv_craft";

// The generated declarations (crates/xiv-craft-wasm/src/lib.rs publishes them) are the source of
// the solver's progress and settings shapes.
export type { SearchProgress, SolverSessionSettings } from "@/wasm/xiv_craft";

type WasmModule = typeof import("@/wasm/xiv_craft");

/**
 * An error after which the WASM instance cannot be trusted: a trap (the crate is built with
 * `panic = "abort"`, so a panic ends in `unreachable`), or wasm-bindgen refusing a re-entered
 * object. The caller drops the instance; `resetWasmEngine` (or a fresh worker) loads a new one.
 */
export function isFatalEngineError(err: unknown): boolean {
  if (typeof WebAssembly !== "undefined" && err instanceof WebAssembly.RuntimeError) return true;
  const message = err instanceof Error ? err.message : String(err);
  return /unreachable|recursive use of an object|memory access out of bounds|null pointer passed to rust|table index is out of bounds/i.test(
    message,
  );
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

/** The typed engine over an instantiated module; the declarations carry the DTO types, so no casts. */
export function createWasmEngine(mod: WasmModule): WasmEngine {
  return {
    initialState: (recipe, stats, startingQuality) =>
      mod.initial_state(recipe, stats, startingQuality),
    executeAction: (state, actionId, stats, recipe) =>
      mod.execute_action(state, actionId, stats, recipe),
    availableActions: (state, stats, recipe) =>
      mod.available_actions(state, stats, recipe),
    startingQuality: (recipe, hqIngredients) =>
      mod.starting_quality(recipe, hqIngredients),
    simulate: (recipe, stats, actionIds, options, hqIngredients) =>
      mod.simulate(
        recipe,
        stats,
        Int32Array.from(actionIds),
        options ?? null,
        hqIngredients ?? null,
      ),
    exportMacro: (actionIds, options) =>
      mod.export_macro(Int32Array.from(actionIds), options ?? null),
    actionCatalog: (level) => mod.action_catalog(level ?? null),
    greedySolve: (recipe, stats, startingQuality, constraints) =>
      mod.greedy_solve(recipe, stats, startingQuality, constraints ?? null),
    createSolverSession: (maxNodes, earlyTermination) => {
      const session = new mod.SolverSession(maxNodes, earlyTermination ?? null);
      return {
        begin: (recipe, stats, startingQuality, constraints, settings) =>
          session.begin(recipe, stats, startingQuality, constraints, settings),
        run: (maxIterations) => session.run(maxIterations),
        progress: () => session.progress(),
        finish: () => session.finish(),
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
        return createWasmEngine(mod);
      })
      .catch((err) => {
        enginePromise = null;
        throw err;
      });
  }
  return enginePromise;
}

/**
 * Forgets the instance after a fatal error, so the next `loadWasmEngine` instantiates a fresh
 * one. The module's glue keeps one instance per JS context, so `default()` returns the same
 * (trapped) instance; a second instantiation needs the bytes again, which `default()` fetches
 * from the (service-worker-cached) asset.
 */
export function resetWasmEngine(): void {
  enginePromise = null;
  loadedAtMs = null;
  instantiateMs = null;
}
