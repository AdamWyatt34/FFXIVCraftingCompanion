/// <reference lib="webworker" />

/**
 * The solver worker. Owns one WASM instance and one MCTS session; the main thread talks to it
 * through `src/lib/solver-client.ts` with the messages in `src/types/solver-worker.ts`.
 *
 * MCTS runs in slices of roughly SLICE_TARGET_MS and yields to the event loop between them,
 * which is where cancel messages are read and progress is posted. Only one MCTS search is
 * alive at a time: a new one cancels the current search and starts once its loop has exited,
 * so the shared tree is never reset under a live search. Greedy solves take milliseconds and
 * share no state, so they run inline when their message arrives.
 */

import { loadWasmEngine } from "@/lib/wasm-engine";
import type { SolverSession, WasmEngine } from "@/lib/wasm-engine";
import type { SolverJob, SolverResult } from "@/types/solver";
import type {
  SolveWorkerRequest,
  SolverWorkerRequest,
  SolverWorkerResponse,
} from "@/types/solver-worker";

declare const self: DedicatedWorkerGlobalScope;

/** About 130 bytes per node: ~80 MB at the cap, which phones tolerate; a full 10 s search would want 2-3x that. */
const MAX_NODES = 600_000;
/**
 * The crate's early-termination rule counts iterations without improvement, tuned for the C#
 * solver's ~12k iterations per second. At 250k-1M per second here it would stop a search after
 * ~20 ms with the greedy seed, so it is lifted and replaced by a wall-clock rule: stop once the
 * best rotation has not improved for STALL_MS. Improvements arrive in bursts seconds apart (1 s and 2 s
 * stalls were tried and cut searches off just before an improvement), so this is the floor and the
 * budget (see solver-client.ts) is the ceiling.
 */
const CRATE_EARLY_TERMINATION_OFF = 0xffff_ffff;
const STALL_MS = 3_000;
const SLICE_TARGET_MS = 25;
const PROGRESS_INTERVAL_MS = 100;
const MIN_SLICE_ITERATIONS = 200;
const MAX_SLICE_ITERATIONS = 100_000;

function post(message: SolverWorkerResponse): void {
  self.postMessage(message);
}

const engineReady: Promise<WasmEngine> = loadWasmEngine();
engineReady.then(
  () => post({ type: "ready" }),
  (err) => post({ type: "error", id: "", message: errorMessage(err) }),
);

let session: SolverSession | null = null;
let jobCounter = 0;

interface ActiveSearch {
  id: string;
  cancelled: boolean;
}
let active: ActiveSearch | null = null;
let pending: SolveWorkerRequest | null = null;

function errorMessage(err: unknown): string {
  return err instanceof Error ? err.message : String(err);
}

function toJob(result: SolverResult, elapsedMs: number): SolverJob {
  return {
    jobId: `local-${++jobCounter}`,
    status: result.errorMessage === null ? "Completed" : "Failed",
    result,
    elapsedMs: Math.round(elapsedMs),
    errorMessage: result.errorMessage,
  };
}

/**
 * A macrotask boundary without setTimeout's nested-timer clamp, so cancel messages queued
 * during the slice are handled before the next one starts.
 */
function yieldToEventLoop(): Promise<void> {
  return new Promise((resolve) => {
    const channel = new MessageChannel();
    channel.port1.onmessage = () => {
      channel.port1.close();
      resolve();
    };
    channel.port2.postMessage(null);
  });
}

async function runGreedy(request: SolveWorkerRequest): Promise<void> {
  try {
    const engine = await engineReady;
    const startingQuality = request.hqIngredients?.length
      ? engine.startingQuality(request.recipe, request.hqIngredients)
      : 0;
    const started = performance.now();
    const result = engine.greedySolve(
      request.recipe,
      request.stats,
      startingQuality,
      request.constraints,
    );
    post({
      type: "result",
      id: request.id,
      job: toJob(result, performance.now() - started),
    });
  } catch (err) {
    post({ type: "error", id: request.id, message: errorMessage(err) });
  }
}

async function runMcts(request: SolveWorkerRequest): Promise<void> {
  const search: ActiveSearch = { id: request.id, cancelled: false };
  active = search;
  try {
    const engine = await engineReady;
    session ??= engine.createSolverSession(
      MAX_NODES,
      CRATE_EARLY_TERMINATION_OFF,
    );
    const startingQuality = request.hqIngredients?.length
      ? engine.startingQuality(request.recipe, request.hqIngredients)
      : 0;

    session.begin(
      request.recipe,
      request.stats,
      startingQuality,
      request.constraints,
      {
        timeBudgetMs: request.settings.timeBudgetMs,
        randomSeed: request.settings.randomSeed,
      },
    );

    let iterationsPerSlice = 500;
    let lastProgressAt = 0;
    let bestQuality = -1;
    let bestSteps = Number.MAX_SAFE_INTEGER;
    let lastImprovementAt = performance.now();
    for (;;) {
      const sliceStarted = performance.now();
      const progress = session.run(iterationsPerSlice);
      const sliceMs = Math.max(performance.now() - sliceStarted, 1);
      iterationsPerSlice = Math.min(
        MAX_SLICE_ITERATIONS,
        Math.max(
          MIN_SLICE_ITERATIONS,
          Math.round((iterationsPerSlice * SLICE_TARGET_MS) / sliceMs),
        ),
      );

      const now = performance.now();
      if (
        progress.bestQualityPercent > bestQuality ||
        (progress.bestQualityPercent === bestQuality &&
          progress.bestSteps < bestSteps)
      ) {
        bestQuality = progress.bestQualityPercent;
        bestSteps = progress.bestSteps;
        lastImprovementAt = now;
      }
      const stalled = now - lastImprovementAt >= STALL_MS;
      if (
        progress.done ||
        stalled ||
        now - lastProgressAt >= PROGRESS_INTERVAL_MS
      ) {
        lastProgressAt = now;
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

      await yieldToEventLoop();
      if (search.cancelled) {
        post({ type: "cancelled", id: request.id });
        return;
      }
    }

    const result = session.finish();
    post({
      type: "result",
      id: request.id,
      job: toJob(result, result.metrics.solveTimeMs),
    });
  } catch (err) {
    post({ type: "error", id: request.id, message: errorMessage(err) });
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

self.onmessage = (event: MessageEvent<SolverWorkerRequest>) => {
  const message = event.data;
  switch (message.type) {
    case "solve":
      handleSolve(message);
      break;
    case "cancel":
      handleCancel(message.id);
      break;
  }
};
