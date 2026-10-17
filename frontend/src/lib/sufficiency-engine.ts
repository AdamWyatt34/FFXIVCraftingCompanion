import { simulateCraft } from "@/lib/crafting-engine";
import { quickSolve } from "@/lib/solver-client";
import type { SufficiencyEngine } from "@/lib/static-data/sufficiency";

/**
 * The stat check's engine calls on the WASM engine: the greedy quick solve (the API's
 * `SolverFactory.Create(Greedy)` with default constraints) and a plain simulation of its rotation.
 */
export const wasmSufficiencyEngine: SufficiencyEngine = {
  async solve(recipeId, stats) {
    const result = await quickSolve({ recipeId, ...stats });
    return { completed: result.errorMessage === null, actionIds: result.actionIds };
  },
  async simulate(recipeId, stats, actionIds) {
    const result = await simulateCraft({ recipeId, stats, actionIds });
    return {
      success: result.success,
      totalSteps: result.totalSteps,
      finalQualityPercent: result.finalQualityPercent,
      finalHQPercent: result.finalHQPercent,
    };
  },
};
