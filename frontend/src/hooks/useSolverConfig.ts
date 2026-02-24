"use client";

import { useState, useCallback, useMemo, useEffect } from "react";
import type { SolveRequest } from "@/types/solver";
import type { CrafterStatsInput, HQIngredientInput } from "@/types/simulator";
import { SOLVER_CONSTANTS, ALGORITHM_LABELS } from "@/lib/constants";

export interface SolverConfig {
  algorithm: "auto" | "greedy" | "mcts";
  timeBudgetMs: number;
  objective: "quality" | "steps";
  targetQualityPercent: number | null;
  allowUnsafeActions: boolean;
}

export interface UseSolverConfigReturn {
  config: SolverConfig;
  setAlgorithm: (alg: SolverConfig["algorithm"]) => void;
  setTimeBudget: (ms: number) => void;
  setObjective: (obj: SolverConfig["objective"]) => void;
  setTargetQuality: (pct: number | null) => void;
  setAllowUnsafe: (allow: boolean) => void;
  resetToDefaults: () => void;
  buildSolveRequest: (recipeId: number, stats: CrafterStatsInput, hqIngredients?: HQIngredientInput[]) => SolveRequest;
  summaryText: string;
}

const STORAGE_KEY = "ffxiv_solver_config";

const DEFAULT_CONFIG: SolverConfig = {
  algorithm: "auto",
  timeBudgetMs: SOLVER_CONSTANTS.DEFAULT_TIME_BUDGET_MS,
  objective: "quality",
  targetQualityPercent: null,
  allowUnsafeActions: false,
};

function loadConfig(): SolverConfig {
  if (typeof window === "undefined") return DEFAULT_CONFIG;
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return DEFAULT_CONFIG;
    const parsed = JSON.parse(raw);
    return {
      algorithm: ["auto", "greedy", "mcts"].includes(parsed.algorithm)
        ? parsed.algorithm
        : DEFAULT_CONFIG.algorithm,
      timeBudgetMs: clampBudget(parsed.timeBudgetMs ?? DEFAULT_CONFIG.timeBudgetMs),
      objective: parsed.objective === "steps" ? "steps" : "quality",
      targetQualityPercent:
        typeof parsed.targetQualityPercent === "number"
          ? Math.min(100, Math.max(1, parsed.targetQualityPercent))
          : null,
      allowUnsafeActions: parsed.allowUnsafeActions === true,
    };
  } catch {
    return DEFAULT_CONFIG;
  }
}

function clampBudget(ms: number): number {
  return Math.min(30000, Math.max(SOLVER_CONSTANTS.MIN_TIME_BUDGET_MS, ms));
}

export function useSolverConfig(): UseSolverConfigReturn {
  const [config, setConfig] = useState<SolverConfig>(loadConfig);

  // Persist to localStorage on change
  useEffect(() => {
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(config));
    } catch {
      // Ignore storage errors
    }
  }, [config]);

  const setAlgorithm = useCallback((algorithm: SolverConfig["algorithm"]) => {
    setConfig((prev) => ({ ...prev, algorithm }));
  }, []);

  const setTimeBudget = useCallback((ms: number) => {
    setConfig((prev) => ({ ...prev, timeBudgetMs: clampBudget(ms) }));
  }, []);

  const setObjective = useCallback((objective: SolverConfig["objective"]) => {
    setConfig((prev) => ({ ...prev, objective }));
  }, []);

  const setTargetQuality = useCallback((pct: number | null) => {
    setConfig((prev) => ({
      ...prev,
      targetQualityPercent:
        pct !== null ? Math.min(100, Math.max(1, pct)) : null,
    }));
  }, []);

  const setAllowUnsafe = useCallback((allow: boolean) => {
    setConfig((prev) => ({ ...prev, allowUnsafeActions: allow }));
  }, []);

  const resetToDefaults = useCallback(() => {
    setConfig(DEFAULT_CONFIG);
  }, []);

  const buildSolveRequest = useCallback(
    (recipeId: number, stats: CrafterStatsInput, hqIngredients?: HQIngredientInput[]): SolveRequest => {
      return {
        recipeId,
        stats,
        constraints: {
          mustComplete: true,
          maximizeQuality: config.objective === "quality",
          minimizeSteps: config.objective === "steps",
          targetQualityPercent: config.targetQualityPercent ?? undefined,
          allowUnsafeActions: config.allowUnsafeActions,
        },
        settings: {
          algorithm: config.algorithm,
          timeBudgetMs:
            config.algorithm === "greedy"
              ? SOLVER_CONSTANTS.QUICK_SOLVE_TIMEOUT_MS
              : config.timeBudgetMs,
        },
        hqIngredients: hqIngredients?.length ? hqIngredients : undefined,
      };
    },
    [config],
  );

  const summaryText = useMemo(() => {
    const algLabel = ALGORITHM_LABELS[config.algorithm]?.label ?? "Auto";
    const budgetSec = Math.round(config.timeBudgetMs / 1000);
    const budgetStr = config.algorithm === "greedy" ? "Instant" : `${budgetSec}s`;
    const objStr =
      config.objective === "steps" ? "Min steps" : "Max quality";
    const parts = [algLabel, budgetStr, objStr];
    if (config.targetQualityPercent !== null) {
      parts.push(`${config.targetQualityPercent}% target`);
    }
    if (config.allowUnsafeActions) {
      parts.push("Risky");
    }
    return parts.join(" · ");
  }, [config]);

  return {
    config,
    setAlgorithm,
    setTimeBudget,
    setObjective,
    setTargetQuality,
    setAllowUnsafe,
    resetToDefaults,
    buildSolveRequest,
    summaryText,
  };
}
