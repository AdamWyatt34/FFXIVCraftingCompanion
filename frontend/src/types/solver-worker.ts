import type { RecipeCraftingParametersDto } from "./api";
import type { CrafterStatsInput, HQIngredientInput } from "./simulator";
import type { SolverConstraints, SolverJob, SolverJobStatus } from "./solver";

/**
 * Messages between the main thread and the solver worker (`src/workers/solver.worker.ts`).
 * The worker owns the WASM solver; the main thread resolves "auto" to an algorithm and the
 * time budget before posting.
 */

export interface SolveWorkerRequest {
  type: "solve";
  id: string;
  algorithm: "greedy" | "mcts";
  recipe: RecipeCraftingParametersDto;
  stats: CrafterStatsInput;
  constraints: SolverConstraints;
  settings: { timeBudgetMs: number; randomSeed?: number };
  hqIngredients?: HQIngredientInput[];
  /** Used when no HQ materials are given; with them the engine works the starting quality out. */
  startingQuality?: number;
}

export interface CancelWorkerRequest {
  type: "cancel";
  id: string;
}

export type SolverWorkerRequest = SolveWorkerRequest | CancelWorkerRequest;

/** What the solver has found so far; the API path reports only status and elapsed time. */
export interface SolverProgress {
  status: SolverJobStatus;
  elapsedMs: number;
  algorithm?: "Greedy" | "MCTS";
  iterations?: number;
  nodesExplored?: number;
  bestSteps?: number;
  bestQualityPercent?: number;
  bestHQPercent?: number;
}

export type SolverWorkerResponse =
  | { type: "ready" }
  | { type: "progress"; id: string; progress: SolverProgress }
  | { type: "result"; id: string; job: SolverJob }
  | { type: "cancelled"; id: string }
  | { type: "error"; id: string; message: string }
  /**
   * The WASM instance trapped (a panic, with `panic = "abort"`) or a session was re-entered: its
   * memory is no longer trusted, every queued solve has failed, and the main thread terminates
   * the worker and starts a fresh one on the next solve.
   */
  | { type: "fatal"; message: string };
