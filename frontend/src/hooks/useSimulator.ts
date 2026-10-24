"use client";

import { useState, useEffect, useRef, useCallback, useMemo } from "react";
import { ApiClientError } from "@/lib/api-client";
import { getRecipe } from "@/lib/game-data";
import {
  getCraftingActions,
  getRecognizedActionIds,
  getAvailableActions as fetchAvailableActions,
  getInitialCraftingState,
  executeCraftingAction,
  exportCraftingMacro,
  getRecipeParameters,
  prefetchRecipeParameters,
  simulateCraft,
} from "@/lib/crafting-engine";
import { loadWasmEngine } from "@/lib/wasm-engine";
import { useProfile } from "@/hooks/useProfile";
import { SIMULATOR_CONSTANTS } from "@/lib/constants";
import {
  inputsForRecipe,
  inputsFromSetup,
  NO_HQ,
  NO_STARTING_QUALITY,
  startingQualityOf,
  storedCraftResumes,
  storedSetup,
  type SimulatorSetup,
  type StartingQualityInputs,
  type StartingQualityStatus,
} from "@/lib/simulator-setup";
import {
  applyConsumables,
  getCraftingConsumables,
  NO_CONSUMABLES,
  sanitizeSelection,
} from "@/lib/static-data/consumables";
import type { CraftingConsumableDto, RecipeSummaryDto } from "@/types/api";
import type {
  ConsumableSelection,
  CraftingState,
  CrafterStatsInput,
  ActionInfo,
  ActionAvailability,
  HQIngredientInput,
  StepResult,
} from "@/types/simulator";
import wasmBuild from "@/wasm/BUILD.json";

// Module-level cache for action list (rarely changes)
let cachedActions: ActionInfo[] | null = null;
let cachedActionsLevel: number | undefined = undefined;

const SESSION_STORAGE_KEY = "ffxiv_simulator_state";
const SESSION_EXPIRY_MS = 30 * 60 * 1000; // 30 minutes
// Rewritten by wasm:build whenever crates/ changes.
const ENGINE_BUILD = wasmBuild.cratesTreeHash;

const NO_OPTIONS: CraftingConsumableDto[] = [];
const NO_DROPPED: number[] = [];

async function hqStartingQuality(recipeId: number, hqIngredients: HQIngredientInput[]): Promise<number> {
  const [engine, parameters] = await Promise.all([loadWasmEngine(), getRecipeParameters(recipeId)]);
  return engine.startingQuality(parameters, hqIngredients);
}

interface SimulatorSessionData {
  recipeId: number;
  recipeSummary: RecipeSummaryDto;
  /** The base stats, without food or medicine. */
  crafterStats: CrafterStatsInput;
  consumables?: ConsumableSelection;
  /** The manual value; the craft starts from the HQ materials while any is picked. */
  startingQuality?: number;
  hqIngredients?: HQIngredientInput[];
  stateHistory: StepResult[];
  currentState: CraftingState;
  initialState: CraftingState;
  /** The engine build that worked out the states. */
  engine?: string;
  timestamp: number;
}

export type RestoredCraftClosure = "consumables" | "engine";

export interface UseSimulatorReturn {
  // Setup state
  selectedRecipe: RecipeSummaryDto | null;
  /** The stats the craft runs on: the base stats plus the selected meal and medicine; null while a selection waits for the catalogue. */
  crafterStats: CrafterStatsInput | null;
  /** The stats as the player entered them, without consumables. */
  baseStats: CrafterStatsInput | null;
  /** The requested selection, for the picker and its notices. */
  consumables: ConsumableSelection;
  /** The selection crafterStats carries, none while the catalogue has failed; a craft or solve is saved, shared and replayed with it. */
  appliedConsumables: ConsumableSelection;
  /** Empty until loaded, and for a data set exported without consumables. */
  consumableOptions: CraftingConsumableDto[];
  /** Item ids of chosen consumables the catalogue cannot honour; they add nothing. */
  droppedConsumables: number[];
  /** The catalogue failed to load, so a chosen meal or medicine adds nothing; false again once it loads. */
  consumablesFailed: boolean;
  /** Why a restored craft was closed, not resumed: the catalogue fails to load or no longer lists its food or medicine, or another engine build stored it. */
  restoredCraftClosed: RestoredCraftClosure | null;
  setConsumables: (selection: ConsumableSelection) => void;
  isSetupComplete: boolean;
  /** The quality the craft starts at: from the HQ materials when any is selected, else the manual value. */
  startingQuality: number;
  /** The manual value alone, which a replay carries next to the HQ materials. */
  manualStartingQuality: number;
  /** Not "ready" while the HQ materials' quality is being worked out or could not be; a solve waits for it. */
  startingQualityStatus: StartingQualityStatus;
  /** The manual starting quality, clamped to the recipe's quality; it applies while no HQ material is selected. */
  setStartingQuality: (value: number) => void;
  hqIngredients: HQIngredientInput[];
  setHqIngredients: (hqIngredients: HQIngredientInput[]) => void;

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
  /** True once the recipe is the simulator's; false when it failed to load or a later recipe load superseded it. */
  setRecipe: (recipeId: number) => Promise<boolean>;
  setStats: (stats: CrafterStatsInput) => void;
  setStatsFromUrl: (stats: CrafterStatsInput, setup?: SimulatorSetup) => void;
  /** `stats` are base stats; pass the simulator's own setup through replaySetup. Resolves as setRecipe does. */
  loadSharedRotation: (
    recipeId: number,
    stats: CrafterStatsInput,
    actionIds: number[],
    setup?: SimulatorSetup,
  ) => Promise<boolean>;
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

/** `restoreSession` is false when the page opens on a link, whose setup wins over a stored craft. */
export function useSimulator({ restoreSession }: { restoreSession: boolean }): UseSimulatorReturn {
  // Setup
  const [selectedRecipe, setSelectedRecipe] = useState<RecipeSummaryDto | null>(
    null,
  );
  const [recipeId, setRecipeId] = useState<number | null>(null);
  const [baseStats, setBaseStats] = useState<CrafterStatsInput | null>(null);
  const [selectedConsumables, setSelectedConsumables] =
    useState<ConsumableSelection>(NO_CONSUMABLES);
  // null until the catalogue loads, so a selection from a link is only judged against a real one
  const [consumableOptions, setConsumableOptions] = useState<
    CraftingConsumableDto[] | null
  >(null);
  const [consumablesFailed, setConsumablesFailed] = useState(false);
  const [restoredCraftClosed, setRestoredCraftClosed] = useState<RestoredCraftClosure | null>(null);
  const [recipeQuality, setRecipeQuality] = useState<number | null>(null);
  const [startingQualityInputs, setStartingQualityInputs] =
    useState<StartingQualityInputs>(NO_STARTING_QUALITY);
  const [hqQuality, setHqQuality] = useState<{
    ingredients: HQIngredientInput[];
    value: number | null;
  } | null>(null);

  const { selection: consumables, dropped: droppedConsumables } = useMemo(
    () =>
      consumableOptions
        ? sanitizeSelection(selectedConsumables, consumableOptions)
        : { selection: selectedConsumables, dropped: NO_DROPPED },
    [selectedConsumables, consumableOptions],
  );
  const catalogueFailed = consumablesFailed && consumableOptions === null;
  const appliedConsumables = catalogueFailed ? NO_CONSUMABLES : consumables;
  // Until the catalogue answers, a chosen meal or medicine would silently add nothing.
  const awaitingCatalogue =
    consumableOptions === null &&
    !catalogueFailed &&
    (selectedConsumables.meal !== null || selectedConsumables.medicine !== null);
  const crafterStats = useMemo(
    () =>
      baseStats && !awaitingCatalogue
        ? applyConsumables(baseStats, appliedConsumables, consumableOptions ?? NO_OPTIONS)
        : null,
    [baseStats, awaitingCatalogue, appliedConsumables, consumableOptions],
  );

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

  // Counts recipe loads; only the latest writes the recipe, so one that answers late never replaces a later pick.
  const recipeLoadRef = useRef(0);

  // Profile auto-fill
  const { profile } = useProfile();

  const { hqIngredients, manualStartingQuality, startingQuality, startingQualityStatus } =
    startingQualityOf(recipeId, startingQualityInputs, hqQuality);

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
      !baseStats ||
      !currentState ||
      !initialState
    )
      return;
    try {
      const data: SimulatorSessionData = {
        recipeId,
        recipeSummary: selectedRecipe,
        crafterStats: baseStats,
        consumables: appliedConsumables,
        startingQuality: manualStartingQuality,
        hqIngredients,
        stateHistory,
        currentState,
        initialState,
        engine: ENGINE_BUILD,
        timestamp: Date.now(),
      };
      sessionStorage.setItem(SESSION_STORAGE_KEY, JSON.stringify(data));
    } catch {
      // sessionStorage may be full or unavailable
    }
  }, [
    recipeId,
    selectedRecipe,
    baseStats,
    appliedConsumables,
    manualStartingQuality,
    hqIngredients,
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

  const sessionRestoredRef = useRef(false);
  // A restored craft offers its actions once crafterStats carries its food and medicine.
  const restoredActionsPendingRef = useRef(false);
  const restoredConsumablesRef = useRef<ConsumableSelection | null>(null);
  useEffect(() => {
    if (sessionRestoredRef.current) return;
    sessionRestoredRef.current = true;
    if (!restoreSession) return;

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

      const restoredRecipeId = data.recipeId;
      const setup = storedSetup(data);
      setSelectedRecipe(data.recipeSummary);
      setRecipeId(restoredRecipeId);
      setBaseStats(data.crafterStats);
      setSelectedConsumables(setup.consumables);
      setStartingQualityInputs(inputsFromSetup(restoredRecipeId, setup));
      // The session keeps only the recipe's summary; its quality bounds the manual starting quality.
      getRecipe(restoredRecipeId)
        .then((recipe) => {
          setRecipeQuality((quality) => quality ?? recipe.quality);
          setStartingQualityInputs((inputs) =>
            inputs.manual.recipeId === restoredRecipeId
              ? inputsForRecipe(inputs, restoredRecipeId, recipe.quality)
              : inputs,
          );
        })
        .catch((err) => console.warn("Restored recipe failed to load:", err));
      // Only the states depend on the engine build; the setup is kept so the craft can start again from it.
      if (!storedCraftResumes(data, ENGINE_BUILD)) {
        sessionStorage.removeItem(SESSION_STORAGE_KEY);
        setRestoredCraftClosed("engine");
        return;
      }
      initialStateRef.current = data.initialState;
      setInitialState(data.initialState);
      setCurrentState(data.currentState);
      setStateHistory(data.stateHistory);
      setIsSimulating(true);
      restoredActionsPendingRef.current = true;
      restoredConsumablesRef.current =
        setup.consumables.meal !== null || setup.consumables.medicine !== null ? setup.consumables : null;
    } catch {
      // corrupt data, ignore
    }
  }, [restoreSession]);

  // Auto-save session whenever simulation state changes
  useEffect(() => {
    if (isSimulating && currentState) {
      saveSession();
    }
  }, [isSimulating, currentState, stateHistory, saveSession]);

  // Auto-fill stats from profile when available and no stats set yet (skip if URL-provided)
  useEffect(() => {
    if (!baseStats && !statsSetByUrlRef.current && profile?.stats) {
      setBaseStats({
        level: profile.activeJobLevel,
        craftsmanship: profile.stats.craftsmanship,
        control: profile.stats.control,
        cp: profile.stats.cp,
      });
    }
  }, [profile, baseStats]);

  useEffect(() => {
    let cancelled = false;
    // Restored states used the meal and medicine; close before actions are offered or a save runs,
    // so the stored session survives for a reload that can apply them.
    const closeRestoredCraftUnlessReproducible = (options: CraftingConsumableDto[] | null) => {
      const restored = restoredConsumablesRef.current;
      if (!restoredActionsPendingRef.current || restored === null) return;
      if (options !== null && sanitizeSelection(restored, options).dropped.length === 0) return;
      restoredActionsPendingRef.current = false;
      initialStateRef.current = null;
      setInitialState(null);
      setCurrentState(null);
      setStateHistory([]);
      setIsSimulating(false);
      setRestoredCraftClosed("consumables");
    };
    getCraftingConsumables()
      .then((options) => {
        if (cancelled) return;
        closeRestoredCraftUnlessReproducible(options);
        setConsumableOptions(options);
      })
      .catch((err) => {
        console.warn("Crafting consumables failed to load:", err);
        if (cancelled) return;
        setConsumablesFailed(true);
        closeRestoredCraftUnlessReproducible(null);
      });
    return () => {
      cancelled = true;
    };
  }, []);

  useEffect(() => {
    if (recipeId === null || hqIngredients.length === 0) return;
    let cancelled = false;
    hqStartingQuality(recipeId, hqIngredients)
      .then((value) => {
        if (!cancelled) setHqQuality({ ingredients: hqIngredients, value });
      })
      .catch((err) => {
        console.warn("Starting quality from HQ materials failed:", err);
        if (!cancelled) setHqQuality({ ingredients: hqIngredients, value: null });
      });
    return () => {
      cancelled = true;
    };
  }, [recipeId, hqIngredients]);

  // Fetch action catalog
  useEffect(() => {
    const level = baseStats?.level;
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
  }, [baseStats?.level]);

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

  useEffect(() => {
    if (!restoredActionsPendingRef.current || !crafterStats || !currentState || recipeId === null) return;
    restoredActionsPendingRef.current = false;
    void refreshAvailableActions(currentState, crafterStats, recipeId);
  }, [crafterStats, currentState, recipeId, refreshAvailableActions]);

  const setRecipe = useCallback(async (id: number): Promise<boolean> => {
    const load = ++recipeLoadRef.current;
    const isLatest = () => load === recipeLoadRef.current;
    setIsLoading(true);
    setError(null);
    try {
      const recipe = await getRecipe(id);
      if (!isLatest()) return false;
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
      setRecipeQuality(recipe.quality);
      setStartingQualityInputs((inputs) => inputsForRecipe(inputs, id, recipe.quality));
      prefetchRecipeParameters(id);
      return true;
    } catch (err) {
      if (!isLatest() || (err instanceof DOMException && err.name === "AbortError")) return false;
      setError(
        err instanceof ApiClientError
          ? err.body.error
          : "Failed to load recipe",
      );
      return false;
    } finally {
      if (isLatest()) setIsLoading(false);
    }
  }, []);

  const setStats = useCallback((stats: CrafterStatsInput) => {
    setBaseStats(stats);
  }, []);

  const setStartingQuality = useCallback(
    (value: number) => {
      const manual = { recipeId, value: Math.max(0, Math.min(value, recipeQuality ?? value)) };
      setStartingQualityInputs((inputs) => ({ ...inputs, manual }));
    },
    [recipeId, recipeQuality],
  );

  const setHqIngredients = useCallback(
    (ingredients: HQIngredientInput[]) => {
      const hq = { recipeId, ingredients: ingredients.length > 0 ? ingredients : NO_HQ };
      setStartingQualityInputs((inputs) => ({ ...inputs, hq }));
    },
    [recipeId],
  );

  // A craft can start before the effect has turned a fresh HQ selection into a quality.
  const resolveStartingQuality = useCallback(
    (): Promise<number> =>
      recipeId !== null && hqIngredients.length > 0
        ? hqStartingQuality(recipeId, hqIngredients)
        : Promise.resolve(manualStartingQuality),
    [recipeId, hqIngredients, manualStartingQuality],
  );

  // A link's starting quality is clamped to the recipe's quality once setRecipe knows it.
  const setStatsFromUrl = useCallback(
    (stats: CrafterStatsInput, setup?: SimulatorSetup) => {
      statsSetByUrlRef.current = true;
      setBaseStats(stats);
      setSelectedConsumables(setup?.consumables ?? NO_CONSUMABLES);
      setStartingQualityInputs(inputsFromSetup(null, setup));
    },
    [],
  );

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
      const quality = await resolveStartingQuality();
      const state = await getInitialCraftingState(
        recipeId,
        crafterStats.level,
        crafterStats.craftsmanship,
        crafterStats.control,
        crafterStats.cp,
        crafterStats.isSpecialist ?? false,
        controller.signal,
        quality,
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
  }, [recipeId, crafterStats, resolveStartingQuality, refreshAvailableActions]);

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
      const quality = await resolveStartingQuality();
      const state = await getInitialCraftingState(
        recipeId,
        crafterStats.level,
        crafterStats.craftsmanship,
        crafterStats.control,
        crafterStats.cp,
        crafterStats.isSpecialist ?? false,
        controller.signal,
        quality,
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
  }, [recipeId, crafterStats, resolveStartingQuality, refreshAvailableActions]);

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
      setup?: SimulatorSetup,
    ): Promise<boolean> => {
      const load = ++recipeLoadRef.current;
      const isLatest = () => load === recipeLoadRef.current;
      let landed = false;
      setIsLoading(true);
      setError(null);

      try {
        // A shared link is user input: keep only ids the engine knows, and never more steps
        // than a craft can hold.
        const known = await getRecognizedActionIds();
        const actionIds = sharedActionIds
          .filter((id) => known.has(id))
          .slice(0, SIMULATOR_CONSTANTS.MAX_ACTIONS_PER_CRAFT);

        // Awaited here so the run below already includes the consumables.
        const [recipe, options] = await Promise.all([
          getRecipe(sharedRecipeId),
          getCraftingConsumables().catch((err) => {
            console.warn("Crafting consumables failed to load:", err);
            return null;
          }),
        ]);
        const selection = setup?.consumables ?? NO_CONSUMABLES;
        const effective = options
          ? applyConsumables(
              stats,
              sanitizeSelection(selection, options).selection,
              options,
            )
          : stats;
        const startingQualityFromSetup = inputsFromSetup(sharedRecipeId, setup, recipe.quality);
        const { manual, hq } = startingQualityFromSetup;
        const sharedStartingQuality =
          hq.ingredients.length > 0
            ? await hqStartingQuality(sharedRecipeId, hq.ingredients)
            : manual.value;
        if (!isLatest()) return false;

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
        setBaseStats(stats);
        // Without the catalogue the run below is on the base stats, so the craft must record no food or medicine.
        if (options) setConsumableOptions(options);
        else setConsumablesFailed(true);
        setSelectedConsumables(selection);
        setRecipeQuality(recipe.quality);
        setStartingQualityInputs(startingQualityFromSetup);
        landed = true;

        // An unusable rotation still leaves the link's recipe, stats and setup to craft from.
        if (actionIds.length === 0) {
          setIsSimulating(false);
          setCurrentState(null);
          setStateHistory([]);
          setAvailableActions([]);
          setError("The shared rotation contains no valid actions.");
          return true;
        }

        // Run the full simulation
        const result = await simulateCraft({
          recipeId: sharedRecipeId,
          stats: effective,
          actionIds,
          startingQuality: sharedStartingQuality,
        });

        // Set initial state from the first step's "before" or calculate it
        const initialSt = await getInitialCraftingState(
          sharedRecipeId,
          effective.level,
          effective.craftsmanship,
          effective.control,
          effective.cp,
          effective.isSpecialist ?? false,
          undefined,
          sharedStartingQuality,
        );
        if (!isLatest()) return false;

        initialStateRef.current = initialSt;
        setInitialState(initialSt);
        setCurrentState(result.finalState);
        setStateHistory(result.steps);
        setIsSimulating(true);

        await refreshAvailableActions(
          result.finalState,
          effective,
          sharedRecipeId,
        );
        return true;
      } catch (err) {
        if (!isLatest()) return false;
        if (err instanceof DOMException && err.name === "AbortError") return landed;
        setError(
          err instanceof ApiClientError
            ? err.body.error
            : "Failed to load shared rotation",
        );
        return landed;
      } finally {
        if (isLatest()) setIsLoading(false);
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
    baseStats,
    consumables,
    appliedConsumables,
    consumableOptions: consumableOptions ?? NO_OPTIONS,
    droppedConsumables,
    consumablesFailed: catalogueFailed,
    restoredCraftClosed,
    setConsumables: setSelectedConsumables,
    isSetupComplete,
    startingQuality,
    manualStartingQuality,
    startingQualityStatus,
    setStartingQuality,
    hqIngredients,
    setHqIngredients,
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
