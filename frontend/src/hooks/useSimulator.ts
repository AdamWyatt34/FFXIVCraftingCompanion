"use client";

import { useState, useEffect, useRef, useCallback, useMemo } from "react";
import { ApiClientError } from "@/lib/api-client";
import { getRecipe } from "@/lib/game-data";
import {
  getCraftingActions,
  getAvailableActions as fetchAvailableActions,
  getInitialCraftingState,
  executeCraftingAction,
  exportCraftingMacro,
  prefetchRecipeParameters,
  simulateCraft,
} from "@/lib/crafting-engine";
import { useProfile } from "@/hooks/useProfile";
import { SIMULATOR_CONSTANTS } from "@/lib/constants";
import type { RecipeSummaryDto } from "@/types/api";
import type {
  CraftingState,
  CrafterStatsInput,
  ActionInfo,
  ActionAvailability,
  StepResult,
} from "@/types/simulator";

// Module-level cache for action list (rarely changes)
let cachedActions: ActionInfo[] | null = null;
let cachedActionsLevel: number | undefined = undefined;

const SESSION_STORAGE_KEY = "ffxiv_simulator_state";
const SESSION_EXPIRY_MS = 30 * 60 * 1000; // 30 minutes

interface SimulatorSessionData {
  recipeId: number;
  recipeSummary: RecipeSummaryDto;
  crafterStats: CrafterStatsInput;
  stateHistory: StepResult[];
  currentState: CraftingState;
  initialState: CraftingState;
  timestamp: number;
}

export interface UseSimulatorReturn {
  // Setup state
  selectedRecipe: RecipeSummaryDto | null;
  crafterStats: CrafterStatsInput | null;
  isSetupComplete: boolean;
  startingQuality: number;
  setStartingQuality: (value: number) => void;

  // Simulation state
  currentState: CraftingState | null;
  initialState: CraftingState | null;
  stateHistory: StepResult[];
  actionIds: number[];
  availableActions: ActionAvailability[];
  isSimulating: boolean;

  // Action catalog
  actions: ActionInfo[];
  isLoadingActions: boolean;

  // Actions
  setRecipe: (recipeId: number) => Promise<void>;
  setStats: (stats: CrafterStatsInput) => void;
  setStatsFromUrl: (stats: CrafterStatsInput) => void;
  loadSharedRotation: (
    recipeId: number,
    stats: CrafterStatsInput,
    actionIds: number[],
  ) => Promise<void>;
  initializeCraft: () => Promise<void>;
  executeAction: (actionId: number) => Promise<void>;
  undoLastAction: () => void;
  undoToStep: (stepNumber: number) => void;
  resetCraft: () => Promise<void>;

  // Results
  macros: string[];
  hqPercent: number;
  exportMacro: () => Promise<void>;
  exitSimulation: () => void;

  // Loading/error
  isLoading: boolean;
  error: string | null;
  clearError: () => void;
}

export function useSimulator(): UseSimulatorReturn {
  // Setup
  const [selectedRecipe, setSelectedRecipe] = useState<RecipeSummaryDto | null>(
    null,
  );
  const [recipeId, setRecipeId] = useState<number | null>(null);
  const [crafterStats, setCrafterStats] = useState<CrafterStatsInput | null>(
    null,
  );
  const [startingQuality, setStartingQuality] = useState(0);

  // Simulation
  const [currentState, setCurrentState] = useState<CraftingState | null>(null);
  const [initialState, setInitialState] = useState<CraftingState | null>(null);
  const [stateHistory, setStateHistory] = useState<StepResult[]>([]);
  const [availableActions, setAvailableActions] = useState<
    ActionAvailability[]
  >([]);
  const [isSimulating, setIsSimulating] = useState(false);
  const initialStateRef = useRef<CraftingState | null>(null);

  // Action catalog
  const [actions, setActions] = useState<ActionInfo[]>(cachedActions ?? []);
  const [isLoadingActions, setIsLoadingActions] = useState(false);

  // Results
  const [macros, setMacros] = useState<string[]>([]);

  // Loading/error
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Abort controllers for in-flight requests
  const initAbortRef = useRef<AbortController | null>(null);
  const actionAbortRef = useRef<AbortController | null>(null);
  const availabilityAbortRef = useRef<AbortController | null>(null);

  // Synchronous guard against double-execution (refs update immediately, unlike state)
  const executingRef = useRef(false);

  // Track whether stats were set from URL (prevents profile auto-fill overwrite)
  const statsSetByUrlRef = useRef(false);

  // Profile auto-fill
  const { profile } = useProfile();

  const isSetupComplete = selectedRecipe !== null && crafterStats !== null;
  const hqPercent = currentState?.hqPercent ?? 0;
  const actionIds = useMemo(
    () => stateHistory.map((s) => s.actionId),
    [stateHistory],
  );

  // Cleanup all abort controllers on unmount
  useEffect(() => {
    return () => {
      initAbortRef.current?.abort();
      actionAbortRef.current?.abort();
      availabilityAbortRef.current?.abort();
    };
  }, []);

  // Session persistence: save
  const saveSession = useCallback(() => {
    if (
      !recipeId ||
      !selectedRecipe ||
      !crafterStats ||
      !currentState ||
      !initialState
    )
      return;
    try {
      const data: SimulatorSessionData = {
        recipeId,
        recipeSummary: selectedRecipe,
        crafterStats,
        stateHistory,
        currentState,
        initialState,
        timestamp: Date.now(),
      };
      sessionStorage.setItem(SESSION_STORAGE_KEY, JSON.stringify(data));
    } catch {
      // sessionStorage may be full or unavailable
    }
  }, [
    recipeId,
    selectedRecipe,
    crafterStats,
    currentState,
    initialState,
    stateHistory,
  ]);

  const clearSession = useCallback(() => {
    try {
      sessionStorage.removeItem(SESSION_STORAGE_KEY);
    } catch {
      // ignore
    }
  }, []);

  // Session persistence: restore on mount (only if no URL params)
  const sessionRestoredRef = useRef(false);
  useEffect(() => {
    if (sessionRestoredRef.current || statsSetByUrlRef.current) return;
    sessionRestoredRef.current = true;

    try {
      const raw = sessionStorage.getItem(SESSION_STORAGE_KEY);
      if (!raw) return;

      const data: SimulatorSessionData = JSON.parse(raw);
      if (Date.now() - data.timestamp > SESSION_EXPIRY_MS) {
        sessionStorage.removeItem(SESSION_STORAGE_KEY);
        return;
      }

      if (
        !data.recipeId ||
        !data.crafterStats ||
        !data.currentState ||
        !data.initialState
      )
        return;

      setSelectedRecipe(data.recipeSummary);
      setRecipeId(data.recipeId);
      setCrafterStats(data.crafterStats);
      initialStateRef.current = data.initialState;
      setInitialState(data.initialState);
      setCurrentState(data.currentState);
      setStateHistory(data.stateHistory);
      setIsSimulating(true);

      refreshAvailableActions(
        data.currentState,
        data.crafterStats,
        data.recipeId,
      );
    } catch {
      // corrupt data, ignore
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // Auto-save session whenever simulation state changes
  useEffect(() => {
    if (isSimulating && currentState) {
      saveSession();
    }
  }, [isSimulating, currentState, stateHistory, saveSession]);

  // Auto-fill stats from profile when available and no stats set yet (skip if URL-provided)
  useEffect(() => {
    if (!crafterStats && !statsSetByUrlRef.current && profile?.stats) {
      setCrafterStats({
        level: profile.activeJobLevel,
        craftsmanship: profile.stats.craftsmanship,
        control: profile.stats.control,
        cp: profile.stats.cp,
      });
    }
  }, [profile, crafterStats]);

  // Fetch action catalog
  useEffect(() => {
    const level = crafterStats?.level;
    if (cachedActions && cachedActionsLevel === level) {
      setActions(cachedActions);
      return;
    }

    let cancelled = false;
    setIsLoadingActions(true);

    getCraftingActions(level)
      .then((data) => {
        if (!cancelled) {
          cachedActions = data;
          cachedActionsLevel = level;
          setActions(data);
        }
      })
      .catch((err) => {
        if (
          !cancelled &&
          !(err instanceof DOMException && err.name === "AbortError")
        ) {
          setError(
            err instanceof ApiClientError
              ? err.body.error
              : "Failed to load actions",
          );
        }
      })
      .finally(() => {
        if (!cancelled) setIsLoadingActions(false);
      });

    return () => {
      cancelled = true;
    };
  }, [crafterStats?.level]);

  // Refresh available actions when state changes
  const refreshAvailableActions = useCallback(
    async (state: CraftingState, stats: CrafterStatsInput, recipe: number) => {
      if (state.status !== "InProgress") {
        setAvailableActions([]);
        return;
      }

      availabilityAbortRef.current?.abort();
      const controller = new AbortController();
      availabilityAbortRef.current = controller;

      try {
        const result = await fetchAvailableActions(
          { state, stats, recipeId: recipe },
          controller.signal,
        );
        if (!controller.signal.aborted) {
          setAvailableActions(result);
        }
      } catch (err) {
        if (err instanceof DOMException && err.name === "AbortError") return;
        // Non-critical: don't set error, just clear availability
        setAvailableActions([]);
      }
    },
    [],
  );

  const setRecipe = useCallback(async (id: number) => {
    setIsLoading(true);
    setError(null);
    try {
      const recipe = await getRecipe(id);
      const summary: RecipeSummaryDto = {
        recipeId: recipe.recipeId,
        craftType: recipe.craftType,
        craftTypeName: recipe.craftType,
        recipeLevel: recipe.recipeLevel,
        requiredCraftsmanship: recipe.requiredCraftsmanship,
        requiredControl: recipe.requiredControl,
        yield: recipe.yield,
      };
      setSelectedRecipe(summary);
      setRecipeId(id);
      prefetchRecipeParameters(id);
    } catch (err) {
      if (err instanceof DOMException && err.name === "AbortError") return;
      setError(
        err instanceof ApiClientError
          ? err.body.error
          : "Failed to load recipe",
      );
    } finally {
      setIsLoading(false);
    }
  }, []);

  const setStats = useCallback((stats: CrafterStatsInput) => {
    setCrafterStats(stats);
  }, []);

  const setStatsFromUrl = useCallback((stats: CrafterStatsInput) => {
    statsSetByUrlRef.current = true;
    setCrafterStats(stats);
  }, []);

  const initializeCraft = useCallback(async () => {
    if (!recipeId || !crafterStats) {
      setError("Recipe and crafter stats are required");
      return;
    }

    initAbortRef.current?.abort();
    const controller = new AbortController();
    initAbortRef.current = controller;

    setIsLoading(true);
    setError(null);

    try {
      const state = await getInitialCraftingState(
        recipeId,
        crafterStats.level,
        crafterStats.craftsmanship,
        crafterStats.control,
        crafterStats.cp,
        crafterStats.isSpecialist ?? false,
        controller.signal,
        startingQuality,
      );

      if (controller.signal.aborted) return;

      initialStateRef.current = state;
      setInitialState(state);
      setCurrentState(state);
      setStateHistory([]);
      setMacros([]);
      setIsSimulating(true);

      await refreshAvailableActions(state, crafterStats, recipeId);
    } catch (err) {
      if (err instanceof DOMException && err.name === "AbortError") return;
      setError(
        err instanceof ApiClientError
          ? err.body.error
          : "Failed to initialize craft",
      );
    } finally {
      if (!controller.signal.aborted) setIsLoading(false);
    }
  }, [recipeId, crafterStats, startingQuality, refreshAvailableActions]);

  const executeAction = useCallback(
    async (actionId: number) => {
      if (!currentState || !crafterStats || !recipeId) return;
      if (currentState.status !== "InProgress") return;
      if (executingRef.current) return; // synchronous double-execution guard

      executingRef.current = true;
      actionAbortRef.current?.abort();
      const controller = new AbortController();
      actionAbortRef.current = controller;

      setIsLoading(true);
      setError(null);

      try {
        const result = await executeCraftingAction(
          {
            state: currentState,
            actionId,
            stats: crafterStats,
            recipeId,
          },
          controller.signal,
        );

        if (controller.signal.aborted) return;

        setStateHistory((prev) => {
          // Guard: skip if we already have a step with this number (prevents duplicate keys)
          if (
            prev.length > 0 &&
            prev[prev.length - 1].stepNumber >= result.stepNumber
          ) {
            return prev;
          }
          return [...prev, result];
        });
        setCurrentState(result.stateAfter);

        await refreshAvailableActions(
          result.stateAfter,
          crafterStats,
          recipeId,
        );
      } catch (err) {
        if (err instanceof DOMException && err.name === "AbortError") return;
        setError(
          err instanceof ApiClientError
            ? err.body.error
            : "Failed to execute action",
        );
      } finally {
        executingRef.current = false;
        if (!controller.signal.aborted) setIsLoading(false);
      }
    },
    [currentState, crafterStats, recipeId, refreshAvailableActions],
  );

  const undoLastAction = useCallback(() => {
    if (stateHistory.length === 0) return;

    const newHistory = stateHistory.slice(0, -1);
    setStateHistory(newHistory);

    const previousState =
      newHistory.length > 0
        ? newHistory[newHistory.length - 1].stateAfter
        : initialStateRef.current;

    if (previousState) {
      setCurrentState(previousState);
      if (crafterStats && recipeId) {
        refreshAvailableActions(previousState, crafterStats, recipeId);
      }
    }
  }, [stateHistory, crafterStats, recipeId, refreshAvailableActions]);

  const undoToStep = useCallback(
    (stepNumber: number) => {
      if (stepNumber < 0) return;

      const newHistory = stateHistory.slice(0, stepNumber);
      setStateHistory(newHistory);

      const previousState =
        newHistory.length > 0
          ? newHistory[newHistory.length - 1].stateAfter
          : initialStateRef.current;

      if (previousState) {
        setCurrentState(previousState);
        if (crafterStats && recipeId) {
          refreshAvailableActions(previousState, crafterStats, recipeId);
        }
      }
    },
    [stateHistory, crafterStats, recipeId, refreshAvailableActions],
  );

  const resetCraft = useCallback(async () => {
    if (!recipeId || !crafterStats) return;

    initAbortRef.current?.abort();
    const controller = new AbortController();
    initAbortRef.current = controller;

    setIsLoading(true);
    setError(null);

    try {
      const state = await getInitialCraftingState(
        recipeId,
        crafterStats.level,
        crafterStats.craftsmanship,
        crafterStats.control,
        crafterStats.cp,
        crafterStats.isSpecialist ?? false,
        controller.signal,
        startingQuality,
      );

      if (controller.signal.aborted) return;

      initialStateRef.current = state;
      setInitialState(state);
      setCurrentState(state);
      setStateHistory([]);
      setMacros([]);

      await refreshAvailableActions(state, crafterStats, recipeId);
    } catch (err) {
      if (err instanceof DOMException && err.name === "AbortError") return;
      setError(
        err instanceof ApiClientError
          ? err.body.error
          : "Failed to reset craft",
      );
    } finally {
      if (!controller.signal.aborted) setIsLoading(false);
    }
  }, [recipeId, crafterStats, startingQuality, refreshAvailableActions]);

  const exportMacro = useCallback(async () => {
    if (stateHistory.length === 0) return;

    setIsLoading(true);
    setError(null);

    try {
      const actionIds = stateHistory.map((step) => step.actionId);
      const result = await exportCraftingMacro({ actionIds });
      setMacros(result.macros);
    } catch (err) {
      if (err instanceof DOMException && err.name === "AbortError") return;
      setError(
        err instanceof ApiClientError
          ? err.body.error
          : "Failed to export macro",
      );
    } finally {
      setIsLoading(false);
    }
  }, [stateHistory]);

  const loadSharedRotation = useCallback(
    async (
      sharedRecipeId: number,
      stats: CrafterStatsInput,
      sharedActionIds: number[],
    ) => {
      setIsLoading(true);
      setError(null);

      try {
        // A shared link is user input: keep only ids the engine knows, and never more steps
        // than a craft can hold.
        const known = new Set((await getCraftingActions()).map((a) => a.id));
        const actionIds = sharedActionIds
          .filter((id) => known.has(id))
          .slice(0, SIMULATOR_CONSTANTS.MAX_ACTIONS_PER_CRAFT);
        if (actionIds.length === 0) {
          setError("The shared rotation contains no valid actions.");
          return;
        }

        // Fetch and set recipe
        const recipe = await getRecipe(sharedRecipeId);
        const summary: RecipeSummaryDto = {
          recipeId: recipe.recipeId,
          craftType: recipe.craftType,
          craftTypeName: recipe.craftType,
          recipeLevel: recipe.recipeLevel,
          requiredCraftsmanship: recipe.requiredCraftsmanship,
          requiredControl: recipe.requiredControl,
          yield: recipe.yield,
        };
        setSelectedRecipe(summary);
        setRecipeId(sharedRecipeId);
        statsSetByUrlRef.current = true;
        setCrafterStats(stats);

        // Run the full simulation
        const result = await simulateCraft({
          recipeId: sharedRecipeId,
          stats,
          actionIds,
        });

        // Set initial state from the first step's "before" or calculate it
        const initialSt = await getInitialCraftingState(
          sharedRecipeId,
          stats.level,
          stats.craftsmanship,
          stats.control,
          stats.cp,
          stats.isSpecialist ?? false,
        );

        initialStateRef.current = initialSt;
        setInitialState(initialSt);
        setCurrentState(result.finalState);
        setStateHistory(result.steps);
        setIsSimulating(true);

        await refreshAvailableActions(result.finalState, stats, sharedRecipeId);
      } catch (err) {
        if (err instanceof DOMException && err.name === "AbortError") return;
        setError(
          err instanceof ApiClientError
            ? err.body.error
            : "Failed to load shared rotation",
        );
      } finally {
        setIsLoading(false);
      }
    },
    [refreshAvailableActions],
  );

  const exitSimulation = useCallback(() => {
    setIsSimulating(false);
    setCurrentState(null);
    setStateHistory([]);
    setAvailableActions([]);
    setMacros([]);
    clearSession();
  }, [clearSession]);

  const clearError = useCallback(() => setError(null), []);

  return {
    selectedRecipe,
    crafterStats,
    isSetupComplete,
    startingQuality,
    setStartingQuality,
    currentState,
    initialState,
    stateHistory,
    actionIds,
    availableActions,
    isSimulating,
    actions,
    isLoadingActions,
    setRecipe,
    setStats,
    setStatsFromUrl,
    loadSharedRotation,
    initializeCraft,
    executeAction,
    undoLastAction,
    undoToStep,
    resetCraft,
    macros,
    hqPercent,
    exportMacro,
    exitSimulation,
    isLoading,
    error,
    clearError,
  };
}
