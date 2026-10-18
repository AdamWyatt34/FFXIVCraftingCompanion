/// <reference lib="webworker" />

/**
 * The solver worker: the WASM engine, the message port and the event-loop yield around
 * `src/lib/solver-runner.ts`, which holds the logic. The main thread talks to it through
 * `src/lib/solver-client.ts` with the messages in `src/types/solver-worker.ts`.
 */

import { loadWasmEngine } from "@/lib/wasm-engine";
import { createSolverRunner } from "@/lib/solver-runner";
import type { SolverWorkerRequest, SolverWorkerResponse } from "@/types/solver-worker";

declare const self: DedicatedWorkerGlobalScope;

function post(message: SolverWorkerResponse): void {
  self.postMessage(message);
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

const engineReady = loadWasmEngine();
engineReady.then(
  () => post({ type: "ready" }),
  (err) => post({ type: "error", id: "", message: err instanceof Error ? err.message : String(err) }),
);

const runner = createSolverRunner({
  engine: engineReady,
  post,
  yieldToEventLoop,
  now: () => performance.now(),
});

self.onmessage = (event: MessageEvent<SolverWorkerRequest>) => {
  runner.handle(event.data);
};
