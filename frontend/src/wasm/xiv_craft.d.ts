/* tslint:disable */
/* eslint-disable */

/**
 * A resumable MCTS search. `begin`, then `run` in slices until the progress says `done` (or the
 * caller decides to stop), then `finish`. The tree is kept between searches.
 */
export class SolverSession {
    free(): void;
    [Symbol.dispose](): void;
    begin(recipe: any, stats: any, starting_quality: number, constraints: any, settings: any): void;
    finish(): any;
    /**
     * `max_nodes` caps the tree (about 130 bytes per node in practice). `early_termination`
     * is the iterations-without-improvement stop; omit it for the C# default of 5000.
     */
    constructor(max_nodes: number, early_termination?: number | null);
    progress(): any;
    run(max_iterations: number): any;
    tree_memory_bytes(): number;
}

export function action_catalog(level?: number | null): any;

export function available_actions(state: any, stats: any, recipe: any): any;

export function execute_action(state: any, action_id: number, stats: any, recipe: any): any;

export function export_macro(action_ids: Int32Array, options: any): any;

/**
 * The greedy solver; `constraints` may be null for the API's quick-solve defaults.
 */
export function greedy_solve(recipe: any, stats: any, starting_quality: number, constraints: any): any;

export function initial_state(recipe: any, stats: any, starting_quality: number): any;

export function simulate(recipe: any, stats: any, action_ids: Int32Array, options: any, hq_ingredients: any): any;

export function starting_quality(recipe: any, hq_ingredients: any): number;

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
