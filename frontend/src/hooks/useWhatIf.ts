"use client";

import { useState, useRef, useCallback, useEffect, useMemo } from "react";
import { ApiClientError } from "@/lib/api-client";
import { getBisStats } from "@/lib/game-data";
import { quickSolve } from "@/lib/solver-client";
import { WHAT_IF_CONSTANTS, WHAT_IF_PRESETS } from "@/lib/constants";
import type { WhatIfStatSet, WhatIfResult, SolverResult } from "@/types/solver";

type VariedStat = "craftsmanship" | "control" | "cp";

function statKey(s: WhatIfStatSet): string {
  return `${s.craftsmanship}-${s.control}-${s.cp}-${s.level}`;
}

// Module-level LRU cache for solver results
const solverCache = new Map<string, SolverResult>();
function cacheGet(key: string): SolverResult | undefined {
  const val = solverCache.get(key);
  if (val) {
    // Move to end (most recently used)
    solverCache.delete(key);
    solverCache.set(key, val);
  }
  return val;
}
function cacheSet(key: string, val: SolverResult): void {
  if (solverCache.size >= WHAT_IF_CONSTANTS.CACHE_MAX) {
    // Delete oldest entry
    const firstKey = solverCache.keys().next().value;
    if (firstKey !== undefined) solverCache.delete(firstKey);
  }
  solverCache.set(key, val);
}

// Module-level cache for BiS stats
let bisCache: {
  key: string;
  stats: { craftsmanship: number; control: number; cp: number };
} | null = null;

export interface UseWhatIfReturn {
  baseStats: WhatIfStatSet | null;
  setBaseStats: (stats: WhatIfStatSet) => void;
  results: WhatIfResult[];
  isAnyLoading: boolean;
  variedStat: VariedStat | null;
  setVariedStat: (stat: VariedStat) => void;
  chartData: Array<{ statValue: number; hqPercent: number; steps: number }>;
  applyPreset: (key: string) => void;
  addCustomEntry: (stats: WhatIfStatSet, label: string) => void;
  clearAll: () => void;
  recipeId: number | null;
  setRecipeId: (id: number) => void;
  activePreset: string | null;
  isBisLoading: boolean;
}

export function useWhatIf(): UseWhatIfReturn {
  const [baseStats, setBaseStatsState] = useState<WhatIfStatSet | null>(null);
  const [results, setResults] = useState<WhatIfResult[]>([]);
  const [variedStat, setVariedStatState] = useState<VariedStat | null>(null);
  const [recipeId, setRecipeIdState] = useState<number | null>(null);
  const [activePreset, setActivePreset] = useState<string | null>(null);
  const [isBisLoading, setIsBisLoading] = useState(false);

  const debounceRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const abortRef = useRef<AbortController | null>(null);
  const nextIdRef = useRef(0);

  // Cleanup on unmount
  useEffect(() => {
    return () => {
      if (debounceRef.current) clearTimeout(debounceRef.current);
      abortRef.current?.abort();
    };
  }, []);

  const solveEntry = useCallback(
    async (entry: WhatIfResult, signal: AbortSignal): Promise<WhatIfResult> => {
      if (!recipeId) return { ...entry, error: "No recipe selected" };

      const key = `${recipeId}-${statKey(entry.stats)}`;
      const cached = cacheGet(key);
      if (cached) {
        return {
          ...entry,
          isLoading: false,
          result: cached,
          hqPercent: cached.metrics.finalHQPercent,
          steps: cached.metrics.totalSteps,
          craftCompleted: cached.metrics.craftCompleted,
        };
      }

      try {
        const result = await quickSolve(
          {
            recipeId,
            craftsmanship: entry.stats.craftsmanship,
            control: entry.stats.control,
            cp: entry.stats.cp,
            level: entry.stats.level,
          },
          signal,
        );

        if (signal.aborted) return entry;

        cacheSet(key, result);
        return {
          ...entry,
          isLoading: false,
          result,
          hqPercent: result.metrics.finalHQPercent,
          steps: result.metrics.totalSteps,
          craftCompleted: result.metrics.craftCompleted,
        };
      } catch (err) {
        if (signal.aborted) return entry;
        if (err instanceof DOMException && err.name === "AbortError")
          return entry;

        const msg =
          err instanceof ApiClientError
            ? err.status === 429
              ? "Too many requests. Try again in a few seconds."
              : err.body.error
            : "Solve failed";
        return { ...entry, isLoading: false, error: msg };
      }
    },
    [recipeId],
  );

  const dispatchSolves = useCallback(
    (entries: WhatIfResult[]) => {
      abortRef.current?.abort();
      const controller = new AbortController();
      abortRef.current = controller;

      // Immediately set entries as loading
      setResults(entries);

      // Solve each entry
      entries.forEach((entry, idx) => {
        if (!entry.isLoading) return; // Already resolved from cache

        solveEntry(entry, controller.signal).then((resolved) => {
          if (controller.signal.aborted) return;
          setResults((prev) => prev.map((r, i) => (i === idx ? resolved : r)));
        });
      });
    },
    [solveEntry],
  );

  const generateDataPoints = useCallback(
    (base: WhatIfStatSet, stat: VariedStat): WhatIfResult[] => {
      const step = WHAT_IF_CONSTANTS.STAT_STEP;
      const count = WHAT_IF_CONSTANTS.MAX_DATA_POINTS;
      const halfCount = Math.floor(count / 2);
      const points: WhatIfResult[] = [];
      const seen = new Set<string>();

      for (let i = -halfCount; i <= halfCount; i++) {
        const offset = i * step;
        const stats: WhatIfStatSet = { ...base };
        stats[stat] = Math.max(1, base[stat] + offset);

        const key = statKey(stats);
        if (seen.has(key)) continue;
        seen.add(key);

        const cacheKey = recipeId ? `${recipeId}-${key}` : key;
        const cached = cacheGet(cacheKey);

        points.push({
          id: `dp-${nextIdRef.current++}`,
          label: offset === 0 ? "Current" : `${offset > 0 ? "+" : ""}${offset}`,
          stats,
          isLoading: !cached,
          error: null,
          result: cached ?? null,
          hqPercent: cached?.metrics.finalHQPercent ?? 0,
          steps: cached?.metrics.totalSteps ?? 0,
          craftCompleted: cached?.metrics.craftCompleted ?? false,
        });
      }
      return points;
    },
    [recipeId],
  );

  const setBaseStats = useCallback(
    (stats: WhatIfStatSet) => {
      setBaseStatsState(stats);
      setActivePreset(null);

      // If there's a varied stat, regenerate data points with debounce
      if (variedStat && recipeId) {
        if (debounceRef.current) clearTimeout(debounceRef.current);
        debounceRef.current = setTimeout(() => {
          const entries = generateDataPoints(stats, variedStat);
          dispatchSolves(entries);
        }, WHAT_IF_CONSTANTS.DEBOUNCE_MS);
      }
    },
    [variedStat, recipeId, generateDataPoints, dispatchSolves],
  );

  const setVariedStat = useCallback(
    (stat: VariedStat) => {
      setVariedStatState(stat);
      if (!baseStats || !recipeId) return;

      if (debounceRef.current) clearTimeout(debounceRef.current);
      const entries = generateDataPoints(baseStats, stat);
      dispatchSolves(entries);
    },
    [baseStats, recipeId, generateDataPoints, dispatchSolves],
  );

  const setRecipeId = useCallback((id: number) => {
    setRecipeIdState(id);
    setResults([]);
    setVariedStatState(null);
  }, []);

  const applyPreset = useCallback(
    (key: string) => {
      if (!baseStats || !recipeId) return;

      setActivePreset(key);

      if (key === "bis") {
        // Fetch BiS stats from gear recommendations
        setIsBisLoading(true);
        const bisCacheKey = `bis-${baseStats.level}`;
        if (bisCache && bisCache.key === bisCacheKey) {
          const bisStats: WhatIfStatSet = {
            ...bisCache.stats,
            level: baseStats.level,
          };
          const entry: WhatIfResult = {
            id: `preset-${nextIdRef.current++}`,
            label: "BiS",
            stats: bisStats,
            isLoading: true,
            error: null,
            result: null,
            hqPercent: 0,
            steps: 0,
            craftCompleted: false,
          };
          setIsBisLoading(false);
          setResults((prev) => {
            const filtered = prev.filter((r) => r.label !== "BiS");
            return [...filtered, entry];
          });
          // Solve it
          const controller = new AbortController();
          abortRef.current = controller;
          solveEntry(entry, controller.signal).then((resolved) => {
            if (!controller.signal.aborted) {
              setResults((prev) =>
                prev.map((r) => (r.id === entry.id ? resolved : r)),
              );
            }
          });
          return;
        }

        // Need to determine the job. We'll try "CRP" as a fallback since we don't
        // have job context here — the caller should set up the proper job.
        getBisStats("CRP", baseStats.level)
          .then((stats) => {
            bisCache = { key: bisCacheKey, stats };
            const bisStats: WhatIfStatSet = {
              ...stats,
              level: baseStats.level,
            };
            const entry: WhatIfResult = {
              id: `preset-${nextIdRef.current++}`,
              label: "BiS",
              stats: bisStats,
              isLoading: true,
              error: null,
              result: null,
              hqPercent: 0,
              steps: 0,
              craftCompleted: false,
            };
            setResults((prev) => {
              const filtered = prev.filter((r) => r.label !== "BiS");
              return [...filtered, entry];
            });
            const controller = new AbortController();
            abortRef.current = controller;
            solveEntry(entry, controller.signal).then((resolved) => {
              if (!controller.signal.aborted) {
                setResults((prev) =>
                  prev.map((r) => (r.id === entry.id ? resolved : r)),
                );
              }
            });
          })
          .catch(() => {
            setResults((prev) => [
              ...prev.filter((r) => r.label !== "BiS"),
              {
                id: `preset-${nextIdRef.current++}`,
                label: "BiS",
                stats: baseStats,
                isLoading: false,
                error: "Failed to fetch BiS stats",
                result: null,
                hqPercent: 0,
                steps: 0,
                craftCompleted: false,
              },
            ]);
          })
          .finally(() => setIsBisLoading(false));
        return;
      }

      const preset = WHAT_IF_PRESETS.find((p) => p.key === key);
      if (!preset) return;

      const adjustedStats: WhatIfStatSet = {
        craftsmanship: baseStats.craftsmanship + preset.buffs.craftsmanship,
        control: baseStats.control + preset.buffs.control,
        cp: baseStats.cp + preset.buffs.cp,
        level: baseStats.level,
      };

      const entry: WhatIfResult = {
        id: `preset-${nextIdRef.current++}`,
        label: preset.label,
        stats: adjustedStats,
        isLoading: true,
        error: null,
        result: null,
        hqPercent: 0,
        steps: 0,
        craftCompleted: false,
      };

      // Check for duplicate stat combos
      const newKey = statKey(adjustedStats);
      setResults((prev) => {
        const existing = prev.find((r) => statKey(r.stats) === newKey);
        if (existing) return prev;
        return [...prev, entry];
      });

      // Solve
      const controller = new AbortController();
      abortRef.current = controller;
      solveEntry(entry, controller.signal).then((resolved) => {
        if (!controller.signal.aborted) {
          setResults((prev) =>
            prev.map((r) => (r.id === entry.id ? resolved : r)),
          );
        }
      });
    },
    [baseStats, recipeId, solveEntry],
  );

  const addCustomEntry = useCallback(
    (stats: WhatIfStatSet, label: string) => {
      if (!recipeId) return;

      const newKey = statKey(stats);
      setResults((prev) => {
        const existing = prev.find((r) => statKey(r.stats) === newKey);
        if (existing) return prev;

        const cacheKey = `${recipeId}-${newKey}`;
        const cached = cacheGet(cacheKey);

        const entry: WhatIfResult = {
          id: `custom-${nextIdRef.current++}`,
          label,
          stats,
          isLoading: !cached,
          error: null,
          result: cached ?? null,
          hqPercent: cached?.metrics.finalHQPercent ?? 0,
          steps: cached?.metrics.totalSteps ?? 0,
          craftCompleted: cached?.metrics.craftCompleted ?? false,
        };

        if (!cached) {
          const controller = new AbortController();
          abortRef.current = controller;
          solveEntry(entry, controller.signal).then((resolved) => {
            if (!controller.signal.aborted) {
              setResults((p) =>
                p.map((r) => (r.id === entry.id ? resolved : r)),
              );
            }
          });
        }

        return [...prev, entry];
      });
    },
    [recipeId, solveEntry],
  );

  const clearAll = useCallback(() => {
    abortRef.current?.abort();
    if (debounceRef.current) clearTimeout(debounceRef.current);
    setResults([]);
    setVariedStatState(null);
    setActivePreset(null);
  }, []);

  const isAnyLoading = results.some((r) => r.isLoading) || isBisLoading;

  const chartData = useMemo(() => {
    if (!variedStat) return [];
    return results
      .filter((r) => !r.isLoading && !r.error && r.result)
      .map((r) => ({
        statValue: r.stats[variedStat],
        hqPercent: r.hqPercent,
        steps: r.steps,
      }))
      .sort((a, b) => a.statValue - b.statValue);
  }, [results, variedStat]);

  return {
    baseStats,
    setBaseStats,
    results,
    isAnyLoading,
    variedStat,
    setVariedStat,
    chartData,
    applyPreset,
    addCustomEntry,
    clearAll,
    recipeId,
    setRecipeId,
    activePreset,
    isBisLoading,
  };
}
