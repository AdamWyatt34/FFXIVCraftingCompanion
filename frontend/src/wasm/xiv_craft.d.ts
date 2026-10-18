/* tslint:disable */
/* eslint-disable */

import type { RecipeCraftingParametersDto } from "../types/api";
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
} from "../types/simulator";
import type { SolverConstraints, SolverResult } from "../types/solver";

/** The simulate endpoint's result without its recipe summary, which the caller fills in. */
export type WasmSimulationResult = Omit<SimulationResult, "recipe"> & { recipe: null };

/** What an MCTS search has found so far (`SearchProgress` in the crate). */
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



/**
 * A resumable MCTS search. `begin`, then `run` in slices until the progress says `done` (or the
 * caller decides to stop), then `finish`. The tree is kept between searches. Bad input is an
 * error from `begin` and leaves the session as it was; a step without a search is an error too.
 */
export class SolverSession {
    free(): void;
    [Symbol.dispose](): void;
    begin(recipe: RecipeCraftingParametersDto, stats: CrafterStatsInput, starting_quality: number, constraints: SolverConstraints | null | undefined, settings: SolverSessionSettings): void;
    finish(): SolverResult;
    /**
     * `max_nodes` caps the tree (about 130 bytes per node in practice). `early_termination`
     * is the iterations-without-improvement stop; omit it for the C# default of 5000.
     */
    constructor(max_nodes: number, early_termination?: number | null);
    progress(): SearchProgress;
    run(max_iterations: number): SearchProgress;
    tree_memory_bytes(): number;
}

export function action_catalog(level?: number | null): ActionInfo[];

export function available_actions(state: CraftingState, stats: CrafterStatsInput, recipe: RecipeCraftingParametersDto): ActionAvailability[];

export function execute_action(state: CraftingState, action_id: number, stats: CrafterStatsInput, recipe: RecipeCraftingParametersDto): StepResult;

export function export_macro(action_ids: Int32Array, options: MacroOptions | null | undefined): ExportMacroResult;

/**
 * The greedy solver; `constraints` may be null for the API's quick-solve defaults.
 */
export function greedy_solve(recipe: RecipeCraftingParametersDto, stats: CrafterStatsInput, starting_quality: number, constraints: SolverConstraints | null | undefined): SolverResult;

export function initial_state(recipe: RecipeCraftingParametersDto, stats: CrafterStatsInput, starting_quality: number): CraftingState;

export function simulate(recipe: RecipeCraftingParametersDto, stats: CrafterStatsInput, action_ids: Int32Array, options: SimulationOptions | null | undefined, hq_ingredients: HQIngredientInput[] | null | undefined): WasmSimulationResult;

/**
 * The crate is built with `panic = "abort"`, so a panic ends in an unreachable trap and the
 * instance's memory is no longer trusted; the hook writes the message to the console first,
 * where the worker's error path would otherwise only see "unreachable executed".
 */
export function start(): void;

export function starting_quality(recipe: RecipeCraftingParametersDto, hq_ingredients: HQIngredientInput[] | null | undefined): number;

export type InitInput = RequestInfo | URL | Response | BufferSource | WebAssembly.Module;

export interface InitOutput {
    readonly memory: WebAssembly.Memory;
    readonly __wbg_solversession_free: (a: number, b: number) => void;
    readonly action_catalog: (a: number) => [number, number, number];
    readonly available_actions: (a: any, b: any, c: any) => [number, number, number];
    readonly execute_action: (a: any, b: number, c: any, d: any) => [number, number, number];
    readonly export_macro: (a: number, b: number, c: any) => [number, number, number];
    readonly greedy_solve: (a: any, b: any, c: number, d: any) => [number, number, number];
    readonly initial_state: (a: any, b: any, c: number) => [number, number, number];
    readonly simulate: (a: any, b: any, c: number, d: number, e: any, f: any) => [number, number, number];
    readonly solversession_begin: (a: number, b: any, c: any, d: number, e: any, f: any) => [number, number];
    readonly solversession_finish: (a: number) => [number, number, number];
    readonly solversession_new: (a: number, b: number) => number;
    readonly solversession_progress: (a: number) => [number, number, number];
    readonly solversession_run: (a: number, b: number) => [number, number, number];
    readonly solversession_tree_memory_bytes: (a: number) => number;
    readonly start: () => void;
    readonly starting_quality: (a: any, b: any) => [number, number, number];
    readonly __wbindgen_malloc: (a: number, b: number) => number;
    readonly __wbindgen_realloc: (a: number, b: number, c: number, d: number) => number;
    readonly __wbindgen_exn_store: (a: number) => void;
    readonly __externref_table_alloc: () => number;
    readonly __wbindgen_externrefs: WebAssembly.Table;
    readonly __externref_table_dealloc: (a: number) => void;
    readonly __wbindgen_start: () => void;
}

export type SyncInitInput = BufferSource | WebAssembly.Module;

/**
 * Instantiates the given `module`, which can either be bytes or
 * a precompiled `WebAssembly.Module`.
 *
 * @param {{ module: SyncInitInput }} module - Passing `SyncInitInput` directly is deprecated.
 *
 * @returns {InitOutput}
 */
export function initSync(module: { module: SyncInitInput } | SyncInitInput): InitOutput;

/**
 * If `module_or_path` is {RequestInfo} or {URL}, makes a request and
 * for everything else, calls `WebAssembly.instantiate` directly.
 *
 * @param {{ module_or_path: InitInput | Promise<InitInput> }} module_or_path - Passing `InitInput` directly is deprecated.
 *
 * @returns {Promise<InitOutput>}
 */
export default function __wbg_init (module_or_path?: { module_or_path: InitInput | Promise<InitInput> } | InitInput | Promise<InitInput>): Promise<InitOutput>;
