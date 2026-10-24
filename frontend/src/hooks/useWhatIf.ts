"use client";

import { useState, useRef, useCallback, useEffect, useMemo } from "react";
import { ApiClientError } from "@/lib/api-client";
import { getBisStats } from "@/lib/game-data";
import { quickSolve } from "@/lib/solver-client";
import { WHAT_IF_CONSTANTS, WHAT_IF_PRESETS } from "@/lib/constants";
import { craftStartKey, type CraftStart } from "@/lib/simulator-setup";
import { applyConsumables } from "@/lib/static-data/consumables";
import type { CraftingConsumableDto } from "@/types/api";
import type { ConsumableSelection } from "@/types/simulator";
import type { WhatIfStatSet, WhatIfResult, SolverResult } from "@/types/solver";

type VariedStat = "craftsmanship" | "control" | "cp";

function statKey(s: WhatIfStatSet): string {
  return `${s.craftsmanship}-${s.control}-${s.cp}-${s.level}`;
}

function cacheKey(recipeId: number, stats: WhatIfStatSet, start: CraftStart): string {
  return `${recipeId}-${statKey(stats)}-${craftStartKey(start)}`;
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
  /** A no-op while the recipe and its craft type are unchanged, so re-syncing never clears results. */
  setRecipeId: (id: number, craftType: string) => void;
  activePreset: string | null;
  isBisLoading: boolean;
}

/** Solves on the simulator's consumables and start quality, so every row compares on the same setup as Quick Solve. */
export function useWhatIf(
  consumables: ConsumableSelection,
  consumableOptions: readonly CraftingConsumableDto[],
  start: CraftStart,
): UseWhatIfReturn {
  const [baseStats, setBaseStatsState] = useState<WhatIfStatSet | null>(null);
  const [results, setResults] = useState<WhatIfResult[]>([]);
  const [variedStat, setVariedStatState] = useState<VariedStat | null>(null);
  const [recipeId, setRecipeIdState] = useState<number | null>(null);
  const [activePreset, setActivePreset] = useState<string | null>(null);
  const [isBisLoading, setIsBisLoading] = useState(false);

  const debounceRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const abortRef = useRef<AbortController | null>(null);
  const nextIdRef = useRef(0);
  // The callbacks read these instead of state so they stay stable: the page syncs into
  // setBaseStats and setRecipeId from effects, and a new identity would re-run those effects.
  const recipeIdRef = useRef<number | null>(null);
  const craftTypeRef = useRef<string | null>(null);
  const variedStatRef = useRef<VariedStat | null>(null);
  const baseStatsRef = useRef<WhatIfStatSet | null>(null);
  const { startingQuality, hqIngredients } = start;
  const startKey = craftStartKey(start);
  const startRef = useRef<CraftStart>({ startingQuality, hqIngredients });
  const startKeyRef = useRef(startKey);

  const changeVariedStat = useCallback((stat: VariedStat | null) => {
    variedStatRef.current = stat;
    setVariedStatState(stat);
  }, []);

  // Cleanup on unmount
  useEffect(() => {
    return () => {
      if (debounceRef.current) clearTimeout(debounceRef.current);
      abortRef.current?.abort();
    };
  }, []);

  const solveEntry = useCallback(
    async (entry: WhatIfResult, signal: AbortSignal): Promise<WhatIfResult> => {
      const recipeId = recipeIdRef.current;
      if (!recipeId) return { ...entry, error: "No recipe selected" };

      const start = startRef.current;
      const key = cacheKey(recipeId, entry.stats, start);
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
            hqIngredients: start.hqIngredients,
            startingQuality: start.startingQuality,
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
    [],
  );

  // Rows added beside the others share one controller, so every reset aborts all of their solves
  // and adding a row never aborts a sibling's, which would leave that row loading.
  const sharedSignal = useCallback((): AbortSignal => {
    if (!abortRef.current || abortRef.current.signal.aborted) abortRef.current = new AbortController();
    return abortRef.current.signal;
  }, []);

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
      const recipeId = recipeIdRef.current;
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

        const cached = recipeId ? cacheGet(cacheKey(recipeId, stats, startRef.current)) : undefined;

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
    [],
  );

  // Rows solved from another start would sit beside a Quick Solve that uses this one.
  useEffect(() => {
    startRef.current = { startingQuality, hqIngredients };
    if (startKeyRef.current === startKey) return;
    startKeyRef.current = startKey;
    abortRef.current?.abort();
    if (debounceRef.current) clearTimeout(debounceRef.current);
    const stat = variedStatRef.current;
    const base = baseStatsRef.current;
    if (stat && base && recipeIdRef.current) {
      dispatchSolves(generateDataPoints(base, stat));
    } else {
      setResults([]);
      setActivePreset(null);
    }
  }, [startKey, startingQuality, hqIngredients, dispatchSolves, generateDataPoints]);

  const setBaseStats = useCallback(
    (stats: WhatIfStatSet) => {
      baseStatsRef.current = stats;
      setBaseStatsState(stats);
      setActivePreset(null);

      // If there's a varied stat, regenerate data points with debounce
      const stat = variedStatRef.current;
      if (stat && recipeIdRef.current) {
        if (debounceRef.current) clearTimeout(debounceRef.current);
        debounceRef.current = setTimeout(() => {
          dispatchSolves(generateDataPoints(stats, stat));
        }, WHAT_IF_CONSTANTS.DEBOUNCE_MS);
      }
    },
    [generateDataPoints, dispatchSolves],
  );

  // setBaseStats' debounce already re-sweeps on every slider move, so re-choosing the same stat has nothing to redo.
  const setVariedStat = useCallback(
    (stat: VariedStat) => {
      if (variedStatRef.current === stat) return;
      changeVariedStat(stat);
      const base = baseStatsRef.current;
      if (!base || !recipeIdRef.current) return;

      if (debounceRef.current) clearTimeout(debounceRef.current);
      dispatchSolves(generateDataPoints(base, stat));
    },
    [changeVariedStat, generateDataPoints, dispatchSolves],
  );

  const setRecipeId = useCallback(
    (id: number, craftType: string) => {
      if (recipeIdRef.current === id && craftTypeRef.current === craftType) return;
      recipeIdRef.current = id;
      craftTypeRef.current = craftType;
      abortRef.current?.abort();
      if (debounceRef.current) clearTimeout(debounceRef.current);
      setRecipeIdState(id);
      setResults([]);
      setActivePreset(null);
      changeVariedStat(null);
    },
    [changeVariedStat],
  );

  const applyPreset = useCallback(
    (key: string) => {
      if (!baseStats || !recipeId) return;

      setActivePreset(key);

      if (key === "bis") {
        const craftType = craftTypeRef.current;
        if (!craftType) return;
        const bisStatsOf = (gear: { craftsmanship: number; control: number; cp: number }): WhatIfStatSet =>
          applyConsumables({ ...gear, level: baseStats.level }, consumables, consumableOptions);
        // Fetch BiS stats from gear recommendations
        setIsBisLoading(true);
        const bisCacheKey = `bis-${craftType}-${baseStats.level}`;
        if (bisCache && bisCache.key === bisCacheKey) {
          const bisStats = bisStatsOf(bisCache.stats);
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
          const signal = sharedSignal();
          solveEntry(entry, signal).then((resolved) => {
            if (!signal.aborted) {
              setResults((prev) =>
                prev.map((r) => (r.id === entry.id ? resolved : r)),
              );
            }
          });
          return;
        }

        getBisStats(craftType, baseStats.level)
          .then((stats) => {
            bisCache = { key: bisCacheKey, stats };
            const bisStats = bisStatsOf(stats);
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
            const signal = sharedSignal();
            solveEntry(entry, signal).then((resolved) => {
              if (!signal.aborted) {
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
      const signal = sharedSignal();
      solveEntry(entry, signal).then((resolved) => {
        if (!signal.aborted) {
          setResults((prev) =>
            prev.map((r) => (r.id === entry.id ? resolved : r)),
          );
        }
      });
    },
    [baseStats, recipeId, solveEntry, sharedSignal, consumables, consumableOptions],
  );

  const addCustomEntry = useCallback(
    (stats: WhatIfStatSet, label: string) => {
      if (!recipeId) return;

      const newKey = statKey(stats);
      setResults((prev) => {
        const existing = prev.find((r) => statKey(r.stats) === newKey);
        if (existing) return prev;

        const cached = cacheGet(cacheKey(recipeId, stats, startRef.current));

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
          const signal = sharedSignal();
          solveEntry(entry, signal).then((resolved) => {
            if (!signal.aborted) {
              setResults((p) =>
                p.map((r) => (r.id === entry.id ? resolved : r)),
              );
            }
          });
        }

        return [...prev, entry];
      });
    },
    [recipeId, solveEntry, sharedSignal],
  );

  const clearAll = useCallback(() => {
    abortRef.current?.abort();
    if (debounceRef.current) clearTimeout(debounceRef.current);
    setResults([]);
    changeVariedStat(null);
    setActivePreset(null);
  }, [changeVariedStat]);

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
