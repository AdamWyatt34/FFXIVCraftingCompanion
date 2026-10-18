/**
 * The solver worker's logic (`src/workers/solver.worker.ts` is the glue around it): one WASM
 * engine, one MCTS session, the messages in `src/types/solver-worker.ts`. Pure in the sense that
 * posting, yielding and the clock are injected, so vitest drives it on the committed WASM.
 *
 * MCTS runs in slices of roughly SLICE_TARGET_MS and yields to the event loop between them,
 * which is where cancel messages are read and progress is posted. Only one MCTS search is
 * alive at a time: a new one cancels the current search and starts once its loop has exited,
 * so the shared tree is never reset under a live search. Greedy solves take milliseconds and
 * share no state, so they run inline when their message arrives.
 *
 * A fatal engine error (see `isFatalEngineError`) ends the runner: the session is freed, a
 * `fatal` message is posted, and every later request is refused, because the instance's memory
 * cannot be trusted. The main thread replaces the worker.
 */

import { isFatalEngineError } from "@/lib/wasm-engine";
import type { SolverSession, WasmEngine } from "@/lib/wasm-engine";
import type { SolverJob, SolverResult } from "@/types/solver";
import type {
  SolveWorkerRequest,
  SolverWorkerRequest,
  SolverWorkerResponse,
} from "@/types/solver-worker";

/** About 130 bytes per node: ~80 MB at the cap, which phones tolerate; a full 10 s search would want 2-3x that. */
export const MAX_NODES = 600_000;
/**
 * The crate's early-termination rule counts iterations without improvement, tuned for the C#
 * solver's ~12k iterations per second. At 250k-1M per second here it would stop a search after
 * ~20 ms with the greedy seed, so it is lifted and replaced by a wall-clock rule: stop once the
 * best rotation has not improved for STALL_MS. Improvements arrive in bursts seconds apart (1 s and 2 s
 * stalls were tried and cut searches off just before an improvement), so this is the floor and the
 * budget (see solver-client.ts) is the ceiling.
 */
export const CRATE_EARLY_TERMINATION_OFF = 0xffff_ffff;
export const STALL_MS = 3_000;
const SLICE_TARGET_MS = 25;
const PROGRESS_INTERVAL_MS = 100;
const MIN_SLICE_ITERATIONS = 200;
const MAX_SLICE_ITERATIONS = 100_000;

export const FATAL_MESSAGE =
  "The solver engine hit an internal error and was restarted. Please try again.";

export interface RunnerHost {
  engine: Promise<WasmEngine>;
  post: (message: SolverWorkerResponse) => void;
  /** A macrotask boundary between MCTS slices, where queued cancels are read. */
  yieldToEventLoop: () => Promise<void>;
  now: () => number;
}

export interface SolverRunner {
  handle(message: SolverWorkerRequest): void;
  /** True once a fatal engine error has been posted; every later request is refused. */
  readonly isDead: boolean;
}

function errorMessage(err: unknown): string {
  return err instanceof Error ? err.message : String(err);
}

export function createSolverRunner(host: RunnerHost): SolverRunner {
  const { post, now } = host;
  let session: SolverSession | null = null;
  let jobCounter = 0;
  let dead = false;

  interface ActiveSearch {
    id: string;
    cancelled: boolean;
  }
  let active: ActiveSearch | null = null;
  let pending: SolveWorkerRequest | null = null;

  function toJob(result: SolverResult, elapsedMs: number): SolverJob {
    return {
      jobId: `local-${++jobCounter}`,
      status: result.errorMessage === null ? "Completed" : "Failed",
      result,
      elapsedMs: Math.round(elapsedMs),
      errorMessage: result.errorMessage,
    };
  }

  function fatal(err: unknown): void {
    dead = true;
    pending = null;
    try {
      session?.free();
    } catch {
      // The instance is gone either way.
    }
    session = null;
    post({ type: "fatal", message: errorMessage(err) });
  }

  /** A failure during a request: fatal ones end the runner, the rest answer the request. */
  function fail(id: string, err: unknown): void {
    if (isFatalEngineError(err)) fatal(err);
    else post({ type: "error", id, message: errorMessage(err) });
  }

  async function runGreedy(request: SolveWorkerRequest): Promise<void> {
    try {
      const engine = await host.engine;
      const startingQuality = request.hqIngredients?.length
        ? engine.startingQuality(request.recipe, request.hqIngredients)
        : 0;
      const started = now();
      const result = engine.greedySolve(
        request.recipe,
        request.stats,
        startingQuality,
        request.constraints,
      );
      post({ type: "result", id: request.id, job: toJob(result, now() - started) });
    } catch (err) {
      fail(request.id, err);
    }
  }

  async function runMcts(request: SolveWorkerRequest): Promise<void> {
    const search: ActiveSearch = { id: request.id, cancelled: false };
    active = search;
    try {
      const engine = await host.engine;
      session ??= engine.createSolverSession(MAX_NODES, CRATE_EARLY_TERMINATION_OFF);
      const startingQuality = request.hqIngredients?.length
        ? engine.startingQuality(request.recipe, request.hqIngredients)
        : 0;

      session.begin(request.recipe, request.stats, startingQuality, request.constraints, {
        timeBudgetMs: request.settings.timeBudgetMs,
        randomSeed: request.settings.randomSeed,
      });

      let iterationsPerSlice = 500;
      let lastProgressAt = 0;
      let bestQuality = -1;
      let bestSteps = Number.MAX_SAFE_INTEGER;
      let lastImprovementAt = now();
      for (;;) {
        const sliceStarted = now();
        const progress = session.run(iterationsPerSlice);
        const sliceMs = Math.max(now() - sliceStarted, 1);
        iterationsPerSlice = Math.min(
          MAX_SLICE_ITERATIONS,
          Math.max(
            MIN_SLICE_ITERATIONS,
            Math.round((iterationsPerSlice * SLICE_TARGET_MS) / sliceMs),
          ),
        );

        const at = now();
        if (
          progress.bestQualityPercent > bestQuality ||
          (progress.bestQualityPercent === bestQuality && progress.bestSteps < bestSteps)
        ) {
          bestQuality = progress.bestQualityPercent;
          bestSteps = progress.bestSteps;
          lastImprovementAt = at;
        }
        const stalled = at - lastImprovementAt >= STALL_MS;
        if (progress.done || stalled || at - lastProgressAt >= PROGRESS_INTERVAL_MS) {
          lastProgressAt = at;
          post({
            type: "progress",
            id: request.id,
            progress: {
              status: "Running",
              elapsedMs: Math.round(progress.elapsedMs),
              algorithm: "MCTS",
              iterations: progress.iterations,
              nodesExplored: progress.nodesExplored,
              bestSteps: progress.bestSteps,
              bestQualityPercent: progress.bestQualityPercent,
              bestHQPercent: progress.bestHQPercent,
            },
          });
        }
        if (progress.done || stalled) break;

        await host.yieldToEventLoop();
        if (search.cancelled) {
          post({ type: "cancelled", id: request.id });
          return;
        }
      }

      const result = session.finish();
      post({ type: "result", id: request.id, job: toJob(result, result.metrics.solveTimeMs) });
    } catch (err) {
      fail(request.id, err);
    } finally {
      if (active === search) active = null;
      // A search queued while this one ran (a re-solve after cancel) starts now.
      if (pending) void drainPending();
    }
  }

  async function drainPending(): Promise<void> {
    while (pending && !active) {
      const next = pending;
      pending = null;
      await runMcts(next);
    }
  }

  function handleSolve(request: SolveWorkerRequest): void {
    if (dead) {
      post({ type: "error", id: request.id, message: FATAL_MESSAGE });
      return;
    }
    if (request.algorithm === "greedy") {
      void runGreedy(request);
      return;
    }
    if (pending) post({ type: "cancelled", id: pending.id });
    pending = request;
    if (active) {
      // The running loop sees the flag at its next slice boundary; drainPending then starts this one.
      active.cancelled = true;
      return;
    }
    void drainPending();
  }

  function handleCancel(id: string): void {
    if (pending?.id === id) {
      pending = null;
      post({ type: "cancelled", id });
      return;
    }
    if (active?.id === id) active.cancelled = true;
  }

  return {
    handle(message) {
      switch (message.type) {
        case "solve":
          handleSolve(message);
          break;
        case "cancel":
          handleCancel(message.id);
          break;
      }
    },
    get isDead() {
      return dead;
    },
  };
}
