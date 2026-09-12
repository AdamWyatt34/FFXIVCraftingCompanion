/**
 * The solver calls, with one signature for both engines. In `wasm` mode a solve runs in the
 * solver worker (`src/workers/solver.worker.ts`) and reports progress; in `api` mode it is the
 * .NET job: submit, poll, cancel. Both resolve to today's `SolverJob` shape.
 *
 * The API's policy is replicated here for the local path so the two can be compared side by
 * side: `SolverFactory.ResolveAlgorithm` (auto is MCTS when quality is maximised or a target is
 * set, else greedy), `AdaptSettings` (the default 10 s budget is scaled by recipe difficulty),
 * and `StartSolveCommand`'s validation, including the 422 stat-minimum check.
 */

import {
  ApiClientError,
  cancelSolveJob,
  getSolveJob,
  quickSolve as apiQuickSolve,
  submitSolveJob,
} from "@/lib/api-client";
import { SOLVER_CONSTANTS } from "@/lib/constants";
import { getRecipeParameters } from "@/lib/crafting-engine";
import { getEngineMode } from "@/lib/engine-mode";
import type { RecipeCraftingParametersDto } from "@/types/api";
import type { CrafterStatsInput } from "@/types/simulator";
import type {
  QuickSolveParams,
  SolveRequest,
  SolverConstraints,
  SolverJob,
  SolverResult,
} from "@/types/solver";
import { isSolverJob } from "@/types/solver";
import type {
  SolveWorkerRequest,
  SolverProgress,
  SolverWorkerRequest,
  SolverWorkerResponse,
} from "@/types/solver-worker";

export interface SolveOptions {
  signal?: AbortSignal;
  onProgress?: (progress: SolverProgress) => void;
}

const DEFAULT_TIME_BUDGET_MS = 10_000;

function abortError(): DOMException {
  return new DOMException("Aborted", "AbortError");
}

function throwIfAborted(signal?: AbortSignal): void {
  if (signal?.aborted) throw abortError();
}

function reject(status: number, message: string): ApiClientError {
  return new ApiClientError(status, { error: message });
}

// ── Worker transport ──

interface Inflight {
  resolve: (job: SolverJob) => void;
  reject: (err: unknown) => void;
  onProgress?: (progress: SolverProgress) => void;
}

let worker: Worker | null = null;
const inflight = new Map<string, Inflight>();
let nextId = 0;

function failAll(err: unknown): void {
  for (const entry of inflight.values()) entry.reject(err);
  inflight.clear();
}

function getWorker(): Worker {
  if (worker) return worker;
  const created = new Worker(
    new URL("../workers/solver.worker.ts", import.meta.url),
  );
  created.onmessage = (event: MessageEvent<SolverWorkerResponse>) => {
    const message = event.data;
    if (message.type === "ready") return;
    if (message.type === "error" && message.id === "") {
      // The engine failed to load; nothing queued can succeed.
      failAll(reject(500, message.message));
      created.terminate();
      worker = null;
      return;
    }
    const entry = inflight.get(message.id);
    if (!entry) return;
    switch (message.type) {
      case "progress":
        entry.onProgress?.(message.progress);
        break;
      case "result":
        inflight.delete(message.id);
        entry.resolve(message.job);
        break;
      case "cancelled":
        inflight.delete(message.id);
        entry.reject(abortError());
        break;
      case "error":
        inflight.delete(message.id);
        entry.reject(reject(400, message.message));
        break;
    }
  };
  created.onerror = (event) => {
    failAll(reject(500, event.message || "Solver worker crashed"));
    created.terminate();
    worker = null;
  };
  worker = created;
  return created;
}

function solveInWorker(
  request: Omit<SolveWorkerRequest, "type" | "id">,
  options: SolveOptions,
): Promise<SolverJob> {
  throwIfAborted(options.signal);
  const id = `w${++nextId}`;
  const target = getWorker();
  return new Promise<SolverJob>((resolve, rejectPromise) => {
    const onAbort = () => {
      target.postMessage({ type: "cancel", id } satisfies SolverWorkerRequest);
      inflight.delete(id);
      rejectPromise(abortError());
    };
    options.signal?.addEventListener("abort", onAbort, { once: true });
    const settle =
      <T>(fn: (value: T) => void) =>
      (value: T) => {
        options.signal?.removeEventListener("abort", onAbort);
        fn(value);
      };
    inflight.set(id, {
      resolve: settle(resolve),
      reject: settle(rejectPromise),
      onProgress: options.onProgress,
    });
    target.postMessage({
      type: "solve",
      id,
      ...request,
    } satisfies SolverWorkerRequest);
  });
}

// ── API policy ──

type RecipeDifficulty = "Trivial" | "Easy" | "Standard" | "Hard" | "Expert";

/** `RecipeDifficultyClassifier.Classify`. */
function classify(
  recipe: RecipeCraftingParametersDto,
  stats: CrafterStatsInput,
): RecipeDifficulty {
  if (recipe.isExpert) return "Expert";
  if (stats.level >= recipe.classJobLevel + 10 && recipe.stars === 0)
    return "Trivial";
  if (stats.level >= recipe.classJobLevel + 5 || recipe.stars === 0)
    return "Easy";
  if (recipe.stars >= 3 || recipe.durability <= 40) return "Hard";
  return "Standard";
}

const ADAPTIVE_BUDGET_MS: Record<RecipeDifficulty, number> = {
  Trivial: 1_000,
  Easy: 3_000,
  Standard: 5_000,
  Hard: 10_000,
  Expert: 15_000,
};

/** `SolverFactory.AdaptSettings`: only the untouched default budget is adapted. */
function adaptBudget(
  timeBudgetMs: number,
  recipe: RecipeCraftingParametersDto,
  stats: CrafterStatsInput,
): number {
  return timeBudgetMs === DEFAULT_TIME_BUDGET_MS
    ? ADAPTIVE_BUDGET_MS[classify(recipe, stats)]
    : timeBudgetMs;
}

/** `SolverFactory.ResolveAlgorithm`. */
function resolveAlgorithm(
  requested: string | undefined,
  constraints: SolverConstraints,
): "greedy" | "mcts" {
  const algorithm = (requested ?? "auto").toLowerCase();
  if (algorithm === "greedy") return "greedy";
  if (algorithm === "mcts") return "mcts";
  return constraints.maximizeQuality || constraints.targetQualityPercent != null
    ? "mcts"
    : "greedy";
}

function validateStats(stats: CrafterStatsInput): void {
  if (stats.level < 1 || stats.level > 100)
    throw reject(400, "Level must be between 1 and 100.");
  if (stats.craftsmanship <= 0 || stats.cp <= 0) {
    throw reject(400, "Craftsmanship and CP must be positive.");
  }
  if (stats.control < 0) throw reject(400, "Control must be non-negative.");
}

function checkRecipeMinimums(
  recipe: RecipeCraftingParametersDto,
  stats: CrafterStatsInput,
): void {
  if (
    recipe.suggestedCraftsmanship > 0 &&
    stats.craftsmanship < recipe.suggestedCraftsmanship
  ) {
    throw reject(
      422,
      `Craftsmanship ${stats.craftsmanship} is below the recipe minimum of ${recipe.suggestedCraftsmanship}.`,
    );
  }
  if (recipe.suggestedControl > 0 && stats.control < recipe.suggestedControl) {
    throw reject(
      422,
      `Control ${stats.control} is below the recipe minimum of ${recipe.suggestedControl}.`,
    );
  }
}

// ── API path ──

function sleep(ms: number, signal?: AbortSignal): Promise<void> {
  return new Promise((resolve, rejectPromise) => {
    const timer = setTimeout(() => {
      signal?.removeEventListener("abort", onAbort);
      resolve();
    }, ms);
    const onAbort = () => {
      clearTimeout(timer);
      rejectPromise(abortError());
    };
    signal?.addEventListener("abort", onAbort, { once: true });
  });
}

async function solveThroughApi(
  request: SolveRequest,
  options: SolveOptions,
): Promise<SolverJob> {
  const { signal, onProgress } = options;
  const response = await submitSolveJob(request, signal);
  if (isSolverJob(response) && response.result) {
    onProgress?.({ status: "Completed", elapsedMs: response.elapsedMs });
    return response;
  }

  const jobId = response.jobId;
  onProgress?.({ status: "Queued", elapsedMs: 0 });
  try {
    for (;;) {
      await sleep(SOLVER_CONSTANTS.POLL_INTERVAL_MS, signal);
      const job = await getSolveJob(jobId, signal);
      onProgress?.({ status: job.status, elapsedMs: job.elapsedMs });
      if (
        job.status === "Completed" ||
        job.status === "Failed" ||
        job.status === "TimedOut"
      ) {
        return job;
      }
    }
  } catch (err) {
    if (signal?.aborted) cancelSolveJob(jobId).catch(() => {});
    throw err;
  }
}

// ── Public API ──

export async function startSolve(
  request: SolveRequest,
  options: SolveOptions = {},
): Promise<SolverJob> {
  // Profile-backed stats are resolved server-side only; that path stays on the API.
  if (getEngineMode() === "api" || request.useProfileStats || !request.stats) {
    return solveThroughApi(request, options);
  }
  const { signal } = options;
  const stats = request.stats;
  const timeBudgetMs = request.settings?.timeBudgetMs ?? DEFAULT_TIME_BUDGET_MS;
  if (timeBudgetMs < 1000 || timeBudgetMs > 60000) {
    throw reject(400, "TimeBudgetMs must be between 1000 and 60000.");
  }
  const maxSteps = request.constraints?.maxSteps ?? 50;
  if (maxSteps < 1 || maxSteps > 100)
    throw reject(400, "MaxSteps must be between 1 and 100.");
  const requestedAlgorithm = request.settings?.algorithm;
  if (
    requestedAlgorithm &&
    !["auto", "greedy", "mcts"].includes(requestedAlgorithm.toLowerCase())
  ) {
    throw reject(
      400,
      `Invalid algorithm '${requestedAlgorithm}'. Valid values: Auto, Greedy, MCTS.`,
    );
  }
  validateStats(stats);

  const recipe = await getRecipeParameters(request.recipeId, signal);
  throwIfAborted(signal);
  checkRecipeMinimums(recipe, stats);

  const constraints: SolverConstraints = { ...request.constraints, maxSteps };
  return solveInWorker(
    {
      algorithm: resolveAlgorithm(requestedAlgorithm, constraints),
      recipe,
      stats,
      constraints,
      settings: {
        timeBudgetMs: adaptBudget(timeBudgetMs, recipe, stats),
        randomSeed: request.settings?.randomSeed,
      },
      hqIngredients: request.hqIngredients,
    },
    options,
  );
}

/** `GET solve/quick`: greedy, default constraints, no HQ materials. */
export async function quickSolve(
  params: QuickSolveParams,
  signal?: AbortSignal,
): Promise<SolverResult> {
  if (getEngineMode() === "api") return apiQuickSolve(params, signal);
  const stats: CrafterStatsInput = {
    level: params.level,
    craftsmanship: params.craftsmanship,
    control: params.control,
    cp: params.cp,
  };
  validateStats(stats);
  const recipe = await getRecipeParameters(params.recipeId, signal);
  throwIfAborted(signal);

  const job = await solveInWorker(
    {
      algorithm: "greedy",
      recipe,
      stats,
      constraints: {},
      settings: { timeBudgetMs: SOLVER_CONSTANTS.QUICK_SOLVE_TIMEOUT_MS },
    },
    { signal },
  );
  if (!job.result)
    throw reject(500, job.errorMessage ?? "Quick solve produced no result.");
  return job.result;
}
