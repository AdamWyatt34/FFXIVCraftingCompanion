import { readFileSync } from "node:fs";
import { describe, expect, it, vi } from "vitest";
import * as wasm from "@/wasm/xiv_craft";
import { createWasmEngine, isFatalEngineError, type WasmEngine } from "@/lib/wasm-engine";
import { FATAL_MESSAGE, createSolverRunner } from "@/lib/solver-runner";
import type { RecipeCraftingParametersDto } from "@/types/api";
import type { CrafterStatsInput } from "@/types/simulator";
import type { SolveWorkerRequest, SolverWorkerResponse } from "@/types/solver-worker";

// The C# CraftingTestHelpers recipe and crafter, as the crate's own tests use them.
const RECIPE: RecipeCraftingParametersDto = {
  recipeId: 100,
  recipeLevel: 580,
  difficulty: 3500,
  quality: 7200,
  durability: 70,
  progressDivider: 130,
  progressModifier: 80,
  qualityDivider: 115,
  qualityModifier: 70,
  classJobLevel: 90,
  stars: 2,
  isExpert: false,
  conditionsFlag: 0,
  suggestedCraftsmanship: 3700,
  suggestedControl: 3280,
  materialQualityFactor: 75,
  ingredients: [{ itemId: 1, quantity: 2, canBeHQ: true }],
};
const STATS: CrafterStatsInput = { level: 90, craftsmanship: 3800, control: 3600, cp: 600 };

let realEngine: WasmEngine | null = null;

/** The committed artifact, instantiated once for the file (initSync is idempotent). */
function committedEngine(): WasmEngine {
  if (!realEngine) {
    wasm.initSync({ module: readFileSync(new URL("../../wasm/xiv_craft_bg.wasm", import.meta.url)) });
    realEngine = createWasmEngine(wasm);
  }
  return realEngine;
}

function harness(engine: WasmEngine) {
  const messages: SolverWorkerResponse[] = [];
  const runner = createSolverRunner({
    engine: Promise.resolve(engine),
    post: (message) => messages.push(message),
    yieldToEventLoop: () => new Promise((resolve) => setImmediate(resolve)),
    now: () => performance.now(),
  });
  async function next<T extends SolverWorkerResponse["type"]>(type: T, id?: string) {
    await vi.waitFor(
      () => {
        expect(messages.some((m) => m.type === type && (id === undefined || !("id" in m) || m.id === id))).toBe(true);
      },
      { timeout: 10_000, interval: 10 },
    );
    return messages.find((m) => m.type === type && (id === undefined || !("id" in m) || m.id === id)) as Extract<
      SolverWorkerResponse,
      { type: T }
    >;
  }
  return { runner, messages, next };
}

let ids = 0;
function solve(overrides: Partial<SolveWorkerRequest> = {}): SolveWorkerRequest {
  return {
    type: "solve",
    id: `t${++ids}`,
    algorithm: "greedy",
    recipe: RECIPE,
    stats: STATS,
    constraints: { maxSteps: 50 },
    settings: { timeBudgetMs: 1_000, randomSeed: 7 },
    ...overrides,
  };
}

describe("solver runner on the committed WASM", () => {
  it("answers a greedy solve with a completed job", async () => {
    const { runner, next } = harness(committedEngine());
    const request = solve();
    runner.handle(request);
    const result = await next("result", request.id);
    expect(result.job.status).toBe("Completed");
    expect(result.job.result?.actionIds.length).toBeGreaterThan(0);
    expect(result.job.result?.metrics.craftCompleted).toBe(true);
  });

  it("runs MCTS in slices, honours a cancel and completes a budgeted search", async () => {
    const { runner, messages, next } = harness(committedEngine());
    const long = solve({ algorithm: "mcts", settings: { timeBudgetMs: 5_000, randomSeed: 7 } });
    runner.handle(long);
    await next("progress", long.id);
    runner.handle({ type: "cancel", id: long.id });
    await next("cancelled", long.id);
    expect(messages.some((m) => m.type === "result" && m.id === long.id)).toBe(false);

    const short = solve({ algorithm: "mcts", settings: { timeBudgetMs: 300, randomSeed: 7 } });
    runner.handle(short);
    const result = await next("result", short.id);
    expect(result.job.status).toBe("Completed");
    expect(result.job.result?.metrics.algorithmUsed).toBe("MCTS");
    expect(messages.some((m) => m.type === "progress" && m.id === short.id)).toBe(true);
    expect(runner.isDead).toBe(false);
  });

  it("refuses input the engine cannot take with a message instead of a trap", async () => {
    const { runner, next } = harness(committedEngine());
    const request = solve({ algorithm: "mcts", stats: { ...STATS, cp: 40_000 } });
    runner.handle(request);
    const error = await next("error", request.id);
    expect(error.message).toBe("CP 40000 is above the engine's maximum of 32767.");
    expect(runner.isDead).toBe(false);

    const greedy = solve({ stats: { ...STATS, level: 0 } });
    runner.handle(greedy);
    expect((await next("error", greedy.id)).message).toBe("Level must be between 1 and 100.");

    const still = solve();
    runner.handle(still);
    expect((await next("result", still.id)).job.status).toBe("Completed");
  });
});

describe("solver runner after a fatal engine error", () => {
  it("frees the session, posts fatal once and refuses everything after", async () => {
    let freed = 0;
    const trapping = {
      startingQuality: () => 0,
      createSolverSession: () => ({
        begin: () => {
          throw new WebAssembly.RuntimeError("unreachable");
        },
        run: () => {
          throw new Error("not reached");
        },
        progress: () => {
          throw new Error("not reached");
        },
        finish: () => {
          throw new Error("not reached");
        },
        treeMemoryBytes: () => 0,
        free: () => {
          freed += 1;
        },
      }),
    } as unknown as WasmEngine;
    const { runner, messages, next } = harness(trapping);

    const request = solve({ algorithm: "mcts" });
    runner.handle(request);
    const fatal = await next("fatal");
    expect(fatal.message).toContain("unreachable");
    expect(freed).toBe(1);
    expect(runner.isDead).toBe(true);
    expect(messages.filter((m) => m.type === "fatal")).toHaveLength(1);

    const later = solve();
    runner.handle(later);
    expect((await next("error", later.id)).message).toBe(FATAL_MESSAGE);
  });

  it("tells a trap or a re-entered object from an ordinary error", () => {
    expect(isFatalEngineError(new WebAssembly.RuntimeError("unreachable executed"))).toBe(true);
    expect(isFatalEngineError(new Error("recursive use of an object detected which would lead to unsafe aliasing in rust"))).toBe(true);
    expect(isFatalEngineError(new Error("memory access out of bounds"))).toBe(true);
    expect(isFatalEngineError(new Error("Level must be between 1 and 100."))).toBe(false);
    expect(isFatalEngineError("Action ID 9 not found.")).toBe(false);
  });
});
