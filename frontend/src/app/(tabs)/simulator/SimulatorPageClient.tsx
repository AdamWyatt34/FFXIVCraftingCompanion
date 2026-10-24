"use client";

import { useState, useCallback, useEffect, useMemo, useRef, Suspense } from "react";
import { useSearchParams } from "next/navigation";
import dynamic from "next/dynamic";
import { useBreadcrumbs } from "@/contexts/BreadcrumbContext";
import Link from "next/link";
import {
  FlaskConical,
  Check,
  X,
  Wrench,
  Sparkles,
  BookOpen,
  SlidersHorizontal,
  Zap,
} from "lucide-react";
import { useSimulator, type RestoredCraftClosure } from "@/hooks/useSimulator";
import { useSolver } from "@/hooks/useSolver";
import { useSolverConfig } from "@/hooks/useSolverConfig";
import { useWhatIf } from "@/hooks/useWhatIf";
import { submitRotation } from "@/lib/api-client";
import { rotationMetricsFor, rotationSaveErrorMessage } from "@/lib/rotation-metrics";
import { getRecipe, getRecipeCraftingParameters } from "@/lib/game-data";
import { withRequiredStats, type CraftingParameters } from "@/lib/static-data/recipes";
import { useAuth } from "@/contexts/AuthContext";
import { parseRotationFromUrl } from "@/lib/format-utils";
import { validateCrafterStats } from "@/lib/stat-limits";
import { useToast } from "@/components/ui";
import { RecipeSelector } from "@/components/simulator/RecipeSelector";
import { StatsInput } from "@/components/simulator/StatsInput";
import { RecipeParams } from "@/components/simulator/RecipeParams";
import { CraftingStateBar } from "@/components/simulator/CraftingStateBar";
import { ConditionIndicator } from "@/components/simulator/ConditionIndicator";
import { ActiveBuffsDisplay } from "@/components/simulator/ActiveBuffsDisplay";
import { HQProbabilityDisplay } from "@/components/simulator/HQProbabilityDisplay";
import { CraftStatusOverlay } from "@/components/simulator/CraftStatusOverlay";
import { SimulatorHeader } from "@/components/simulator/SimulatorHeader";
import { ActionPanel } from "@/components/simulator/ActionPanel";
import { UndoControls } from "@/components/simulator/UndoControls";
import { StepHistory } from "@/components/simulator/StepHistory";
import { StepHistoryMobile } from "@/components/simulator/StepHistoryMobile";
import { SolverButton } from "@/components/solver/SolverButton";
import { SolverConfigPanel } from "@/components/solver/SolverConfigPanel";
import { SolverProgressOverlay } from "@/components/solver/SolverProgressOverlay";
import { LoadingSkeleton } from "@/components/ui/LoadingStates";
import { buildShareUrl } from "@/lib/format-utils";
import { buildSolverShareUrl, decodeShareContext, parseSolverShareUrl } from "@/lib/rotation-codec";
import { librarySaveProblem, replaySetup, shareContextOf, solvedSetupOf, type SolvedSetup } from "@/lib/simulator-setup";

// Lazy-load heavy components that are conditionally rendered
const MacroExportPanel = dynamic(
  () =>
    import("@/components/simulator/MacroExportPanel").then((m) => ({
      default: m.MacroExportPanel,
    })),
  { ssr: false },
);
const HQIngredientSelector = dynamic(
  () =>
    import("@/components/simulator/HQIngredientSelector").then((m) => ({
      default: m.HQIngredientSelector,
    })),
  { ssr: false },
);
const RotationResultPanel = dynamic(
  () =>
    import("@/components/solver/RotationResultPanel").then((m) => ({
      default: m.RotationResultPanel,
    })),
  { ssr: false },
);
const RotationReplay = dynamic(
  () =>
    import("@/components/solver/RotationReplay").then((m) => ({
      default: m.RotationReplay,
    })),
  { ssr: false },
);
const WhatIfPanel = dynamic(
  () =>
    import("@/components/solver/WhatIfPanel").then((m) => ({
      default: m.WhatIfPanel,
    })),
  { ssr: false },
);
import type { RecipeDto, CraftType } from "@/types/api";
import type { CrafterStatsInput } from "@/types/simulator";

const CONSUMABLES_UNAPPLIED = "Food and medicine couldn't load, so your selection isn't applied.";
const HQ_QUALITY_PENDING = "Working out the starting quality from the HQ materials.";
const HQ_QUALITY_FAILED = "The starting quality from these HQ materials couldn't be worked out; change the picks to try again.";
const RECIPE_LOADING = "Loading the recipe…";
const RESTORED_CRAFT_CLOSED: Record<RestoredCraftClosure, string> = {
  consumables: "The craft in progress was closed because its food or medicine couldn't load.",
  engine: "The craft in progress was closed because the crafting engine was updated; start it again from the same setup.",
};

interface StatTargetProblem {
  severity: "required" | "recommended";
  message: string;
}

function findStatTargetProblem(
  targets: CraftingParameters | null,
  stats: CrafterStatsInput | null,
): StatTargetProblem | null {
  if (!targets || !stats) return null;
  const checks = [
    {
      name: "craftsmanship",
      value: stats.craftsmanship,
      required: targets.requiredCraftsmanship,
      recommended: targets.suggestedCraftsmanship,
    },
    {
      name: "control",
      value: stats.control,
      required: targets.requiredControl,
      recommended: targets.suggestedControl,
    },
  ];
  const belowRequired = checks.find((c) => c.required > 0 && c.value < c.required);
  if (belowRequired) {
    return {
      severity: "required",
      message: `Your ${belowRequired.name} ${belowRequired.value} is below this recipe's required ${belowRequired.required}.`,
    };
  }
  const belowRecommended = checks.find((c) => c.recommended > 0 && c.value < c.recommended);
  if (belowRecommended) {
    return {
      severity: "recommended",
      message: `Your ${belowRecommended.name} ${belowRecommended.value} is below the recommended ${belowRecommended.recommended}; Quick Synthesis and some rotations may fail.`,
    };
  }
  return null;
}

function SectionHeader({
  number,
  title,
  isComplete,
}: {
  number: number;
  title: string;
  isComplete?: boolean;
}) {
  return (
    <div className="flex items-center gap-3 mb-3">
      <div
        className={`w-7 h-7 rounded-full flex items-center justify-center text-sm font-bold shrink-0 ${
          isComplete
            ? "bg-emerald-500 text-white"
            : "bg-bg-elevated text-text-muted"
        }`}
      >
        {isComplete ? <Check size={14} /> : number}
      </div>
      <h2 className="text-lg font-semibold text-text-primary">{title}</h2>
    </div>
  );
}

function SimulatorPageContent() {
  const searchParams = useSearchParams();
  const opensLink = useMemo(
    () =>
      parseSolverShareUrl(searchParams) !== null ||
      Boolean(searchParams.get("recipeId") || searchParams.get("craftsmanship")),
    [searchParams],
  );
  const simulator = useSimulator({ restoreSession: !opensLink });
  const solver = useSolver();
  const solverConfig = useSolverConfig();
  const whatIf = useWhatIf(simulator.appliedConsumables, simulator.consumableOptions, {
    startingQuality: simulator.startingQuality,
    hqIngredients: simulator.hqIngredients,
  });
  const { user } = useAuth();
  const [fullRecipe, setFullRecipe] = useState<RecipeDto | null>(null);
  const [craftingParameters, setCraftingParameters] = useState<CraftingParameters | null>(null);
  const [showMacroPanel, setShowMacroPanel] = useState(false);
  const { setBreadcrumbs } = useBreadcrumbs();

  useEffect(() => {
    setBreadcrumbs([{ label: "Simulator" }]);
  }, [setBreadcrumbs]);
  const [shareConfirmation, setShareConfirmation] = useState(false);
  const [showSolverOverlay, setShowSolverOverlay] = useState(false);
  const [showRotationResult, setShowRotationResult] = useState(false);
  const [showReplay, setShowReplay] = useState(false);
  const [replayInitialStep, setReplayInitialStep] = useState<
    number | undefined
  >(undefined);
  const [solvedSetup, setSolvedSetup] = useState<SolvedSetup | null>(null);
  const [isSavingToLibrary, setIsSavingToLibrary] = useState(false);
  const [saveToLibrarySuccess, setSaveToLibrarySuccess] = useState(false);
  const [isSavingManual, setIsSavingManual] = useState(false);
  const [saveManualSuccess, setSaveManualSuccess] = useState(false);
  const [setupMode, setSetupMode] = useState<"craft" | "solve" | "whatif">(
    "craft",
  );
  const hasInitializedFromUrlRef = useRef(false);
  const autoSolveTriggeredRef = useRef(false);
  // Counts recipe picks, so a link's recipe that loads after one never replaces it.
  const recipeSelectionRef = useRef(0);
  const { toast } = useToast();

  // URL-driven initialization
  useEffect(() => {
    if (hasInitializedFromUrlRef.current) return;

    // The setup cards read the full recipe, which the simulator's own load does not hand back. It
    // is set once that load has set the recipe, so the stats form mounts with the link's stats.
    const loadShared = (...args: Parameters<typeof simulator.loadSharedRotation>) => {
      const selection = recipeSelectionRef.current;
      return simulator
        .loadSharedRotation(...args)
        .then(async (landed) => {
          if (!landed) return;
          const recipe = await getRecipe(args[0]);
          if (recipeSelectionRef.current === selection) setFullRecipe(recipe);
        })
        .catch((err) => console.warn("Shared recipe failed to load:", err));
    };

    // A solver share link (?solve=1&r=...) carries the recipe, the stats and the packed rotation.
    const shared = parseSolverShareUrl(searchParams);
    if (shared) {
      hasInitializedFromUrlRef.current = true;
      const { consumables, isSpecialist, startingQuality } = shared.context;
      loadShared(shared.recipeId, { ...shared.stats, isSpecialist }, shared.actionIds, {
        consumables,
        startingQuality,
      });
      return;
    }

    const recipeIdParam = searchParams.get("recipeId");
    const craftsmanshipParam = searchParams.get("craftsmanship");
    const controlParam = searchParams.get("control");
    const cpParam = searchParams.get("cp");
    const levelParam = searchParams.get("level");
    const rotationParam = searchParams.get("rotation");

    if (!opensLink) return;

    hasInitializedFromUrlRef.current = true;

    // Link input is untrusted; a recipe id must be a positive safe integer before any request uses it.
    const linkedRecipeId = recipeIdParam ? Number(recipeIdParam) : null;
    const recipeId =
      linkedRecipeId !== null && Number.isSafeInteger(linkedRecipeId) && linkedRecipeId > 0 ? linkedRecipeId : null;
    if (linkedRecipeId !== null && recipeId === null) {
      toast("The recipe in this link is invalid and was ignored.", "error", 6000);
    }
    const hasStats =
      craftsmanshipParam && controlParam && cpParam && levelParam;
    const context = decodeShareContext(searchParams);
    const setup = { consumables: context.consumables, startingQuality: context.startingQuality };

    let stats: CrafterStatsInput | null = hasStats
      ? {
          level: Number(levelParam),
          craftsmanship: Number(craftsmanshipParam),
          control: Number(controlParam),
          cp: Number(cpParam),
          isSpecialist: context.isSpecialist,
        }
      : null;

    // Stats in a link are user input; out-of-range values are reported and left out rather
    // than handed to the engine.
    const statsProblem = stats ? validateCrafterStats(stats) : null;
    if (statsProblem) {
      toast(`The stats in this link were ignored: ${statsProblem}`, "error", 6000);
      stats = null;
    }

    const rotationIds = parseRotationFromUrl(rotationParam);
    if (rotationParam && !rotationIds) {
      toast("The rotation in this link is invalid or too long and was ignored.", "error", 6000);
    }

    const solveParam = searchParams.get("solve") === "true";
    const whatifParam = searchParams.get("whatif") === "true";

    // If we have all three (recipe + stats + rotation), load as shared rotation
    if (recipeId && stats && rotationIds) {
      loadShared(recipeId, stats, rotationIds, setup);
      return;
    }

    // Otherwise, auto-populate what we have
    if (recipeId) {
      const selection = recipeSelectionRef.current;
      getRecipe(recipeId)
        .then((recipe) => {
          if (recipeSelectionRef.current !== selection) return;
          setFullRecipe(recipe);
          simulator.setRecipe(recipeId);
        })
        .catch((err) => {
          console.warn("Linked recipe failed to load:", err);
          toast("The recipe in this link could not be loaded.", "error", 6000);
        });
    }

    if (stats) {
      simulator.setStatsFromUrl(stats, setup);
    }

    if (whatifParam) {
      setSetupMode("whatif");
    } else if (solveParam) {
      setSetupMode("solve");
    }
  }, [searchParams, opensLink, simulator, toast]);

  const simulatorRecipeId = simulator.selectedRecipe?.recipeId;
  useEffect(() => {
    if (simulatorRecipeId === undefined) return;
    let cancelled = false;
    getRecipe(simulatorRecipeId)
      .then((recipe) => {
        if (!cancelled) setFullRecipe((current) => current ?? recipe);
      })
      .catch((err) => console.warn("Restored recipe failed to load:", err));
    return () => {
      cancelled = true;
    };
  }, [simulatorRecipeId]);

  // Two syncs keyed on primitives: a stats change must not reset the what-if results, and a
  // re-render must not re-run either sync, which would cancel the debounced solves.
  const { setBaseStats: setWhatIfBaseStats, setRecipeId: setWhatIfRecipe } = whatIf;
  const whatIfRecipeId = simulator.selectedRecipe?.recipeId;
  const whatIfCraftType = simulator.selectedRecipe?.craftType;
  useEffect(() => {
    if (whatIfRecipeId === undefined || whatIfCraftType === undefined) return;
    setWhatIfRecipe(whatIfRecipeId, whatIfCraftType);
  }, [whatIfRecipeId, whatIfCraftType, setWhatIfRecipe]);

  const { craftsmanship, control, cp, level } = simulator.crafterStats ?? {};
  useEffect(() => {
    if (craftsmanship === undefined || control === undefined || cp === undefined || level === undefined) return;
    setWhatIfBaseStats({ craftsmanship, control, cp, level });
  }, [craftsmanship, control, cp, level, setWhatIfBaseStats]);

  useEffect(() => {
    if (!fullRecipe) return;
    let cancelled = false;
    getRecipeCraftingParameters(fullRecipe.recipeId)
      .then((parameters) => {
        if (!cancelled) setCraftingParameters(withRequiredStats(parameters, fullRecipe));
      })
      .catch((err) => {
        console.warn("Recipe crafting parameters failed to load:", err);
      });
    return () => {
      cancelled = true;
    };
  }, [fullRecipe]);
  const recipeParameters =
    fullRecipe && craftingParameters?.recipeId === fullRecipe.recipeId ? craftingParameters : null;
  const statTargetProblem = findStatTargetProblem(recipeParameters, simulator.crafterStats);
  const startingQualityHold =
    simulator.startingQualityStatus === "pending"
      ? HQ_QUALITY_PENDING
      : simulator.startingQualityStatus === "failed"
        ? HQ_QUALITY_FAILED
        : undefined;
  // A picked recipe shows before the simulator has loaded it; a solve in between would run the previous recipe.
  const recipeLoading = fullRecipe !== null && fullRecipe.recipeId !== simulator.selectedRecipe?.recipeId;
  const solveBlock = recipeLoading
    ? { reason: RECIPE_LOADING }
    : statTargetProblem?.severity === "required"
      ? { reason: statTargetProblem.message, describedBy: "stat-target-problem" }
      : startingQualityHold
        ? { reason: startingQualityHold, describedBy: "starting-quality-status" }
        : undefined;
  const solveBlockedReason = solveBlock?.reason;

  // Navigation warning during active simulation
  useEffect(() => {
    if (!simulator.isSimulating || simulator.stateHistory.length === 0) return;
    const handler = (e: BeforeUnloadEvent) => {
      e.preventDefault();
    };
    window.addEventListener("beforeunload", handler);
    return () => window.removeEventListener("beforeunload", handler);
  }, [simulator.isSimulating, simulator.stateHistory.length]);

  const { restoredCraftClosed } = simulator;
  useEffect(() => {
    if (restoredCraftClosed) toast(RESTORED_CRAFT_CLOSED[restoredCraftClosed], "info", 6000);
  }, [restoredCraftClosed, toast]);

  // A solver result belongs to its recipe; loading it under another recipe would replay one recipe's actions on the other.
  const forgetSolve = useCallback(() => {
    solver.clearResult();
    setSolvedSetup(null);
    setShowRotationResult(false);
    setShowReplay(false);
    setShowSolverOverlay(false);
  }, [solver]);

  const handleRecipeSelected = useCallback(
    async (recipe: RecipeDto) => {
      const selection = ++recipeSelectionRef.current;
      // A failed pick leaves the simulator on the recipe it held; the setup must show that one.
      const held = fullRecipe?.recipeId === simulator.selectedRecipe?.recipeId ? fullRecipe : null;
      forgetSolve();
      setFullRecipe(recipe);
      const landed = await simulator.setRecipe(recipe.recipeId);
      if (!landed && recipeSelectionRef.current === selection) setFullRecipe(held);
    },
    [simulator, forgetSolve, fullRecipe],
  );

  const { setHqIngredients, setStartingQuality } = simulator;
  const handleRecipeCleared = useCallback(() => {
    recipeSelectionRef.current++;
    forgetSolve();
    setFullRecipe(null);
    setHqIngredients([]);
    setStartingQuality(0);
  }, [setHqIngredients, setStartingQuality, forgetSolve]);

  const handleStatsChange = useCallback(
    (stats: CrafterStatsInput) => {
      simulator.setStats(stats);
    },
    [simulator],
  );

  const handleStartCrafting = useCallback(async () => {
    await simulator.initializeCraft();
  }, [simulator]);

  const onRecipeSelected = useCallback(
    (recipe: RecipeDto) => {
      if (!recipe || !recipe.recipeId) {
        handleRecipeCleared();
        return;
      }
      handleRecipeSelected(recipe);
    },
    [handleRecipeSelected, handleRecipeCleared],
  );

  const handleShareRotation = useCallback(async () => {
    if (!simulator.selectedRecipe || !simulator.baseStats) return;

    const url = buildShareUrl({
      recipeId: simulator.selectedRecipe.recipeId,
      stats: simulator.baseStats,
      actionIds: simulator.actionIds,
      context: {
        consumables: simulator.appliedConsumables,
        isSpecialist: simulator.baseStats.isSpecialist ?? false,
        // The start the craft ran from, which the live setup may no longer match.
        startingQuality: simulator.initialState?.quality ?? simulator.startingQuality,
      },
    });

    try {
      await navigator.clipboard.writeText(url);
      setShareConfirmation(true);
      setTimeout(() => setShareConfirmation(false), 2000);
    } catch {
      // Fallback: create a temporary textarea to copy
      const textarea = document.createElement("textarea");
      textarea.value = url;
      textarea.style.position = "fixed";
      textarea.style.opacity = "0";
      document.body.appendChild(textarea);
      textarea.select();
      document.execCommand("copy");
      document.body.removeChild(textarea);
      setShareConfirmation(true);
      setTimeout(() => setShareConfirmation(false), 2000);
    }
  }, [
    simulator.selectedRecipe,
    simulator.baseStats,
    simulator.appliedConsumables,
    simulator.initialState,
    simulator.startingQuality,
    simulator.actionIds,
  ]);

  // ── Solver handlers ──

  // Every solve entry point checks the block, not only the buttons that display its reason.
  const solveRefused = useCallback((): boolean => {
    if (solveBlockedReason === undefined) return false;
    toast(solveBlockedReason, "error");
    return true;
  }, [solveBlockedReason, toast]);

  const runAutoSolve = useCallback(
    async (onStart?: () => void) => {
      if (solveRefused()) return;
      const solved = solvedSetupOf(simulator);
      if (!solved) return;
      setSolvedSetup(solved);
      const request = solverConfig.buildSolveRequest(
        solved.recipeId,
        solved.crafterStats,
        solved.hqIngredients.length > 0 ? solved.hqIngredients : undefined,
        solved.startingQuality,
      );
      onStart?.();
      await solver.startSolve(request);
    },
    [solveRefused, simulator, solverConfig, solver],
  );

  const handleAutoSolve = useCallback(() => runAutoSolve(), [runAutoSolve]);

  // Read when the timer fires: the handler is new every render, and depending on it would restart the wait.
  const runAutoSolveRef = useRef(runAutoSolve);
  useEffect(() => {
    runAutoSolveRef.current = runAutoSolve;
  }, [runAutoSolve]);

  const recipeParametersLoaded = recipeParameters !== null;
  useEffect(() => {
    if (autoSolveTriggeredRef.current) return;
    if (setupMode !== "solve") return;
    if (!simulator.isSetupComplete) return;
    // A block that clears, such as a pending HQ quality, delays the solve; one that stays prevents it. The
    // required stats are known only once the recipe's parameters are.
    if (solveBlockedReason !== undefined || !recipeParametersLoaded) return;

    // Latched when a solve starts, so a setup the timer finds unready can still solve on a later render.
    const timer = setTimeout(() => {
      void runAutoSolveRef.current(() => {
        autoSolveTriggeredRef.current = true;
      });
    }, 100);
    return () => clearTimeout(timer);
  }, [setupMode, simulator.isSetupComplete, solveBlockedReason, recipeParametersLoaded]);

  const handleQuickSolve = useCallback(async () => {
    if (solveRefused()) return;
    const solved = solvedSetupOf(simulator);
    if (!solved) return;
    setSolvedSetup(solved);
    await solver.startQuickSolve({
      recipeId: solved.recipeId,
      craftsmanship: solved.crafterStats.craftsmanship,
      control: solved.crafterStats.control,
      cp: solved.crafterStats.cp,
      level: solved.crafterStats.level,
      hqIngredients: solved.hqIngredients,
      startingQuality: solved.startingQuality,
    });
  }, [solveRefused, simulator, solver]);

  const handleAutoSolveFromCraft = useCallback(
    () => runAutoSolve(() => setShowSolverOverlay(true)),
    [runAutoSolve],
  );

  const handleLoadPreview = useCallback(async () => {
    const result = solver.quickResult;
    if (!result || !solvedSetup) return;
    const { stats, setup } = replaySetup(solvedSetup);
    solver.cancelSolve();
    setShowSolverOverlay(false);
    await simulator.loadSharedRotation(solvedSetup.recipeId, stats, result.actionIds, setup);
  }, [solver, simulator, solvedSetup]);

  const handleViewRotation = useCallback(() => {
    setShowRotationResult(true);
    setShowSolverOverlay(false);
  }, []);

  // The solver result belongs to the craft being left, so setup never shows it again.
  const handleBackToSetup = useCallback(() => {
    simulator.exitSimulation();
    forgetSolve();
  }, [simulator, forgetSolve]);

  const handleCancelSolve = useCallback(async () => {
    await solver.cancelSolve();
    setShowSolverOverlay(false);
  }, [solver]);

  // ── Rotation result panel handlers ──

  const handleOpenReplay = useCallback(() => {
    setShowReplay(true);
    setReplayInitialStep(undefined);
  }, []);

  const handleJumpToStep = useCallback((stepIndex: number) => {
    setShowReplay(true);
    setReplayInitialStep(stepIndex);
  }, []);

  const handleCloseReplay = useCallback(() => {
    setShowReplay(false);
    setReplayInitialStep(undefined);
  }, []);

  const handleLoadIntoSimulator = useCallback(async () => {
    const result = solver.solverResult ?? solver.quickResult;
    if (!result || !solvedSetup) return;
    const { stats, setup } = replaySetup(solvedSetup);
    await simulator.loadSharedRotation(solvedSetup.recipeId, stats, result.actionIds, setup);
    solver.clearResult();
    setShowRotationResult(false);
    setShowReplay(false);
  }, [solver, simulator, solvedSetup]);

  const handleSolverShare = useCallback(async () => {
    const result = solver.solverResult ?? solver.quickResult;
    if (!result || !solvedSetup) return;

    const url = buildSolverShareUrl({
      recipeId: solvedSetup.recipeId,
      stats: solvedSetup.baseStats,
      actionIds: result.actionIds,
      algorithm: result.metrics.algorithmUsed,
      context: shareContextOf(solvedSetup),
    });

    try {
      await navigator.clipboard.writeText(url);
      setShareConfirmation(true);
      setTimeout(() => setShareConfirmation(false), 2000);
    } catch {
      const textarea = document.createElement("textarea");
      textarea.value = url;
      textarea.style.position = "fixed";
      textarea.style.opacity = "0";
      document.body.appendChild(textarea);
      textarea.select();
      document.execCommand("copy");
      document.body.removeChild(textarea);
      setShareConfirmation(true);
      setTimeout(() => setShareConfirmation(false), 2000);
    }
  }, [solver, solvedSetup]);

  const handleDismissResult = useCallback(() => {
    setShowRotationResult(false);
    solver.clearResult();
  }, [solver]);

  const handleSaveToLibrary = useCallback(async () => {
    const result = solver.solverResult ?? solver.quickResult;
    if (!result || !solvedSetup) return;
    const problem = librarySaveProblem(solvedSetup.startingQuality);
    if (problem) {
      toast(problem, "error");
      return;
    }
    setIsSavingToLibrary(true);
    try {
      const replay = {
        recipeId: solvedSetup.recipeId,
        actionIds: result.actionIds,
        stats: solvedSetup.crafterStats,
      };
      await submitRotation({
        ...replay,
        metrics: await rotationMetricsFor(replay),
        source: "Solver",
        algorithm: result.metrics.algorithmUsed,
      });
      setSaveToLibrarySuccess(true);
      setTimeout(() => setSaveToLibrarySuccess(false), 3000);
    } catch (error) {
      toast(rotationSaveErrorMessage(error), "error");
    } finally {
      setIsSavingToLibrary(false);
    }
  }, [solver, solvedSetup, toast]);

  const handleSaveManualToLibrary = useCallback(async () => {
    if (
      !simulator.selectedRecipe ||
      !simulator.crafterStats ||
      simulator.actionIds.length === 0
    )
      return;
    const problem = librarySaveProblem(simulator.initialState?.quality ?? 0);
    if (problem) {
      toast(problem, "error");
      return;
    }
    setIsSavingManual(true);
    try {
      const replay = {
        recipeId: simulator.selectedRecipe.recipeId,
        actionIds: simulator.actionIds,
        stats: simulator.crafterStats,
      };
      await submitRotation({
        ...replay,
        metrics: await rotationMetricsFor(replay),
        source: "Manual",
      });
      setSaveManualSuccess(true);
      setTimeout(() => setSaveManualSuccess(false), 3000);
    } catch (error) {
      toast(rotationSaveErrorMessage(error), "error");
    } finally {
      setIsSavingManual(false);
    }
  }, [simulator.selectedRecipe, simulator.crafterStats, simulator.actionIds, simulator.initialState, toast]);

  // Auto-hide overlay when solver completes
  useEffect(() => {
    if (
      solver.solverStatus === "Completed" ||
      solver.solverStatus === "Failed" ||
      solver.solverStatus === "TimedOut"
    ) {
      setShowSolverOverlay(false);
    }
  }, [solver.solverStatus]);

  const hasStats = simulator.crafterStats !== null;
  const consumablesUnapplied =
    simulator.consumablesFailed &&
    (simulator.consumables.meal !== null || simulator.consumables.medicine !== null);

  const lastDeltas =
    simulator.stateHistory.length > 0
      ? simulator.stateHistory[simulator.stateHistory.length - 1].deltas
      : undefined;

  // Screen reader announcement for state deltas
  const lastStep = simulator.stateHistory[simulator.stateHistory.length - 1];
  const srAnnouncement = lastStep
    ? [
        lastStep.deltas.progress !== 0 &&
          `Progress ${lastStep.deltas.progress > 0 ? "increased" : "decreased"} by ${Math.abs(lastStep.deltas.progress)}.`,
        lastStep.deltas.quality !== 0 &&
          `Quality ${lastStep.deltas.quality > 0 ? "increased" : "decreased"} by ${Math.abs(lastStep.deltas.quality)}.`,
        lastStep.deltas.durability !== 0 &&
          `Durability ${lastStep.deltas.durability > 0 ? "increased" : "decreased"} by ${Math.abs(lastStep.deltas.durability)}.`,
        lastStep.deltas.cp !== 0 &&
          `CP ${lastStep.deltas.cp > 0 ? "increased" : "decreased"} by ${Math.abs(lastStep.deltas.cp)}.`,
      ]
        .filter(Boolean)
        .join(" ")
    : "";

  return (
    <div className="p-4">
      {/* Screen reader live region */}
      <div className="sr-only" aria-live="polite" aria-atomic="true">
        {srAnnouncement}
      </div>

      {/* Header — hidden during simulation (SimulatorHeader replaces it) */}
      {!simulator.isSimulating && (
        <div className="max-w-4xl mx-auto">
          <div className="flex items-center gap-2 mb-6">
            <FlaskConical className="text-gold" size={24} />
            <h1 className="text-xl font-bold">Crafting Simulator</h1>
            <Link
              href="/simulator/library"
              className="ml-auto flex items-center gap-1.5 px-3 py-1.5 text-sm text-text-secondary hover:text-gold transition-colors"
            >
              <BookOpen size={16} />
              Library
            </Link>
          </div>
        </div>
      )}

      {/* Error banner */}
      {simulator.error && (
        <div
          className={`${simulator.isSimulating ? "max-w-7xl" : "max-w-4xl"} mx-auto mb-4`}
        >
          <div className="flex items-center gap-2 p-3 bg-red-500/10 border border-red-500/30 rounded-lg text-red-400 text-sm">
            <span className="flex-1">{simulator.error}</span>
            <button
              onClick={simulator.clearError}
              className="hover:text-red-300 transition-colors"
            >
              <X size={16} />
            </button>
          </div>
        </div>
      )}

      {/* Simulation view (when active) */}
      {simulator.isSimulating && simulator.currentState ? (
        <div className="max-w-7xl mx-auto animate-fade-in">
          {/* Simulator header — full width */}
          <SimulatorHeader
            recipe={simulator.selectedRecipe!}
            fullRecipe={fullRecipe ?? undefined}
            state={simulator.currentState}
            onBackToSetup={handleBackToSetup}
          />
          {consumablesUnapplied && (
            <p role="status" className="text-xs text-amber-400 mt-2">
              {CONSUMABLES_UNAPPLIED}
            </p>
          )}

          {/* 2-column grid: main content + step history sidebar */}
          <div className="lg:grid lg:grid-cols-[1fr_320px] lg:gap-6 mt-4">
            {/* Left column: main crafting interface */}
            <div className="space-y-4">
              {/* State bars */}
              <CraftingStateBar
                state={simulator.currentState}
                previousDeltas={lastDeltas}
              />

              {/* Condition + Buffs + HQ row */}
              <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:gap-4">
                <div className="flex items-center gap-2 overflow-x-auto sm:overflow-visible flex-nowrap flex-1 min-w-0">
                  <ConditionIndicator
                    condition={simulator.currentState.condition}
                  />
                  <div className="flex-1 min-w-0">
                    <ActiveBuffsDisplay
                      buffs={simulator.currentState.activeBuffs}
                      innerQuietStacks={simulator.currentState.innerQuietStacks}
                    />
                  </div>
                </div>
                <HQProbabilityDisplay hqPercent={simulator.hqPercent} compact />
              </div>

              {/* Undo controls */}
              <UndoControls
                canUndo={simulator.stateHistory.length > 0}
                stepCount={simulator.stateHistory.length}
                isLoading={simulator.isLoading}
                onReset={simulator.resetCraft}
                onOpenMacroExport={() => setShowMacroPanel(true)}
                onShareRotation={handleShareRotation}
                shareConfirmation={shareConfirmation}
              />

              {/* Solver result (shown in crafting phase after solve completes) */}
              {(solver.solverStatus === "Completed" ||
                solver.solverStatus === "Failed" ||
                solver.solverStatus === "TimedOut") && (
                <SolverButton
                  isSetupComplete={simulator.isSetupComplete}
                  isSimulating={true}
                  solverStatus={solver.solverStatus}
                  solverResult={solver.solverResult}
                  quickResult={solver.quickResult}
                  elapsedMs={solver.elapsedMs}
                  progressPercent={solver.progressPercent}
                  error={solver.error}
                  isSolving={solver.isSolving}
                  onStartSolve={handleAutoSolveFromCraft}
                  onCancelSolve={handleCancelSolve}
                  onViewRotation={handleLoadIntoSimulator}
                  onReSolve={handleAutoSolveFromCraft}
                  onClearResult={solver.clearResult}
                  onLoadPreview={handleLoadPreview}
                  configSummary={solverConfig.summaryText}
                />
              )}

              {/* Mobile step history strip */}
              {simulator.initialState && (
                <div className="lg:hidden">
                  <StepHistoryMobile
                    steps={simulator.stateHistory}
                    initialState={simulator.initialState}
                    onUndoToStep={simulator.undoToStep}
                    onUndoLast={simulator.undoLastAction}
                  />
                </div>
              )}

              {/* Status overlay (completion/failure) */}
              {simulator.currentState.status !== "InProgress" && (
                <CraftStatusOverlay
                  state={simulator.currentState}
                  stateHistory={simulator.stateHistory}
                  onOpenMacroExport={() => setShowMacroPanel(true)}
                  onReset={simulator.resetCraft}
                  onUndo={simulator.undoLastAction}
                  onShareRotation={handleShareRotation}
                  onSaveToLibrary={user ? handleSaveManualToLibrary : undefined}
                  isSaving={isSavingManual}
                  saveSuccess={saveManualSuccess}
                />
              )}

              {/* Action panel with solver overlay */}
              {simulator.currentState.status === "InProgress" && (
                <div className="relative">
                  {/* Inline solve triggers */}
                  {solver.solverStatus === "Idle" && (
                    <div className="mb-3 flex items-center gap-2">
                      <button
                        onClick={handleQuickSolve}
                        disabled={solveBlockedReason !== undefined}
                        title={solveBlockedReason}
                        className="flex items-center gap-1.5 px-3 py-1.5 bg-bg-elevated text-text-secondary font-medium rounded-lg hover:text-gold hover:border-gold/30 border border-border transition-colors text-sm disabled:opacity-50 disabled:cursor-not-allowed"
                        aria-label="Quick solve from current state"
                      >
                        <Zap size={14} />
                        Quick Solve
                      </button>
                      <button
                        onClick={handleAutoSolveFromCraft}
                        disabled={solveBlockedReason !== undefined}
                        title={solveBlockedReason}
                        className="flex items-center gap-1.5 px-3 py-1.5 bg-gold/10 text-gold font-medium rounded-lg hover:bg-gold/20 border border-gold/30 transition-colors text-sm disabled:opacity-50 disabled:cursor-not-allowed"
                        aria-label="Auto-solve from current state"
                      >
                        <Sparkles size={14} />
                        Auto-Solve
                      </button>
                    </div>
                  )}
                  <ActionPanel
                    actions={simulator.actions}
                    availableActions={simulator.availableActions}
                    isLoading={simulator.isLoading}
                    onExecute={simulator.executeAction}
                    onUndo={simulator.undoLastAction}
                    onReset={simulator.resetCraft}
                  />
                  {showSolverOverlay && solver.isSolving && (
                    <SolverProgressOverlay
                      solverStatus={solver.solverStatus}
                      progress={solver.progress}
                      solverResult={solver.solverResult}
                      quickResult={solver.quickResult}
                      elapsedMs={solver.elapsedMs}
                      progressPercent={solver.progressPercent}
                      error={solver.error}
                      onLoadPreview={handleLoadPreview}
                      onCancelSolve={handleCancelSolve}
                    />
                  )}
                </div>
              )}
            </div>

            {/* Right column: step history sidebar (desktop only) */}
            {simulator.initialState && (
              <div className="hidden lg:block">
                <div className="sticky top-[4.5rem] max-h-[calc(100vh-6rem)] flex flex-col bg-bg-surface border border-border rounded-lg p-3">
                  <StepHistory
                    steps={simulator.stateHistory}
                    initialState={simulator.initialState}
                    onUndoToStep={simulator.undoToStep}
                    onUndoLast={simulator.undoLastAction}
                  />
                </div>
              </div>
            )}
          </div>

          {/* Macro export panel */}
          <MacroExportPanel
            isOpen={showMacroPanel}
            actionIds={simulator.actionIds}
            stateHistory={simulator.stateHistory}
            currentState={simulator.currentState}
            onClose={() => setShowMacroPanel(false)}
          />
        </div>
      ) : (
        /* Setup flow */
        <div className="max-w-4xl mx-auto space-y-6">
          {/* Step 1: Recipe Selection */}
          <section>
            <SectionHeader
              number={1}
              title="Select Recipe"
              isComplete={fullRecipe !== null}
            />
            <RecipeSelector
              selectedRecipe={fullRecipe}
              onRecipeSelected={onRecipeSelected}
            />
          </section>

          {/* Step 2 + 3: Stats and Recipe Parameters */}
          {fullRecipe && (
            <div className="lg:grid lg:grid-cols-2 lg:gap-6 space-y-6 lg:space-y-0 animate-slide-up">
              {/* Step 2: Crafter Stats */}
              <section>
                <SectionHeader
                  number={2}
                  title="Crafter Stats"
                  isComplete={hasStats}
                />
                <StatsInput
                  stats={simulator.baseStats}
                  onStatsChange={handleStatsChange}
                  selectedCraftType={fullRecipe.craftType as CraftType}
                  consumableOptions={simulator.consumableOptions}
                  consumables={simulator.consumables}
                  onConsumablesChange={simulator.setConsumables}
                />
                {simulator.droppedConsumables.length > 0 && (
                  <p role="status" className="text-xs text-amber-400 mt-2">
                    The chosen food or medicine was left out because this game data cannot apply it
                    (item {simulator.droppedConsumables.join(", ")}).
                  </p>
                )}
                {consumablesUnapplied && (
                  <p role="status" className="text-xs text-amber-400 mt-2">
                    {CONSUMABLES_UNAPPLIED}
                  </p>
                )}
                {statTargetProblem && (
                  <p
                    id="stat-target-problem"
                    role="status"
                    className={`text-sm mt-2 ${
                      statTargetProblem.severity === "required" ? "text-red-400" : "text-amber-400"
                    }`}
                  >
                    {statTargetProblem.message}
                  </p>
                )}
              </section>

              {/* Step 3: Recipe Parameters */}
              {hasStats ? (
                <section className="animate-fade-in">
                  <SectionHeader number={3} title="Recipe Parameters" />
                  <RecipeParams
                    recipe={fullRecipe}
                    stats={simulator.crafterStats}
                    targets={recipeParameters}
                  />
                </section>
              ) : (
                <div className="hidden lg:block" />
              )}
            </div>
          )}

          {/* HQ Ingredient Selector */}
          {fullRecipe && hasStats && (
            <section className="animate-slide-up">
              <HQIngredientSelector
                key={fullRecipe.recipeId}
                recipe={fullRecipe}
                value={simulator.hqIngredients}
                onChange={simulator.setHqIngredients}
              />
            </section>
          )}

          {fullRecipe && hasStats && simulator.hqIngredients.length > 0 && (
            <section className="animate-slide-up">
              <div className="bg-bg-surface border border-border rounded-lg p-4">
                <p className="text-sm font-semibold text-text-primary mb-2">
                  Starting Quality
                </p>
                <p id="starting-quality-status" role="status" className="text-sm text-text-primary">
                  {simulator.startingQualityStatus === "failed" ? (
                    <span className="text-red-400">{HQ_QUALITY_FAILED}</span>
                  ) : (
                    <>
                      <span className="font-bold text-gold">
                        {simulator.startingQualityStatus === "pending"
                          ? "…"
                          : simulator.startingQuality.toLocaleString()}
                      </span>{" "}
                      <span className="text-xs text-text-muted">from HQ materials</span>
                    </>
                  )}
                </p>
              </div>
            </section>
          )}
          {fullRecipe && hasStats && simulator.hqIngredients.length === 0 && (
            <section className="animate-slide-up">
              <div className="bg-bg-surface border border-border rounded-lg p-4">
                <label
                  htmlFor="starting-quality"
                  className="block text-sm font-semibold text-text-primary mb-2"
                >
                  Starting Quality
                </label>
                <p className="text-xs text-text-muted mb-3">
                  Quality the craft starts at. Selecting HQ materials works it
                  out for you.
                </p>
                <div className="flex items-center gap-3">
                  <input
                    id="starting-quality"
                    type="number"
                    min={0}
                    value={simulator.startingQuality}
                    onChange={(e) => {
                      const val = Math.max(
                        0,
                        parseInt(e.target.value, 10) || 0,
                      );
                      simulator.setStartingQuality(val);
                    }}
                    className="w-32 px-3 py-1.5 bg-bg-elevated border border-border rounded-lg text-sm text-text-primary focus:outline-none focus:border-gold/50 [appearance:textfield] [&::-webkit-outer-spin-button]:appearance-none [&::-webkit-inner-spin-button]:appearance-none"
                  />
                  {simulator.startingQuality > 0 && (
                    <button
                      onClick={() => simulator.setStartingQuality(0)}
                      className="text-xs text-text-muted hover:text-text-secondary transition-colors"
                    >
                      Reset
                    </button>
                  )}
                </div>
              </div>
            </section>
          )}

          {/* Rotation result panel (shown after solver completes + View Rotation) */}
          {showRotationResult &&
            !showReplay &&
            solver.solverStatus === "Completed" &&
            (solver.solverResult?.simulation ??
              solver.quickResult?.simulation) && (
              <RotationResultPanel
                solverResult={(solver.solverResult ?? solver.quickResult)!}
                quickResult={solver.quickResult}
                simulation={
                  (solver.solverResult?.simulation ??
                    solver.quickResult?.simulation)!
                }
                onReplay={handleOpenReplay}
                onJumpToStep={handleJumpToStep}
                onExportMacro={() => setShowMacroPanel(true)}
                onShare={handleSolverShare}
                onReSolve={handleAutoSolve}
                onLoadIntoSimulator={handleLoadIntoSimulator}
                onDismiss={handleDismissResult}
                shareConfirmation={shareConfirmation}
                onSaveToLibrary={user ? handleSaveToLibrary : undefined}
                saveDisabledReason={librarySaveProblem(solvedSetup?.startingQuality ?? 0)}
                isSaving={isSavingToLibrary}
                saveSuccess={saveToLibrarySuccess}
              />
            )}

          {/* Action buttons — Start Crafting + Quick Solve + Auto-Solve + What If */}
          {simulator.isSetupComplete && !showRotationResult && (
            <div className="animate-slide-up space-y-3">
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                {/* Manual craft button */}
                <button
                  onClick={handleStartCrafting}
                  disabled={simulator.isLoading || solver.isSolving}
                  className="flex items-center justify-center gap-2 py-3 px-6 bg-bg-surface border border-border text-text-primary font-bold rounded-lg hover:border-gold hover:text-gold disabled:opacity-50 disabled:cursor-not-allowed transition-colors text-base"
                >
                  <Wrench size={18} />
                  {simulator.isLoading
                    ? "Initializing..."
                    : "Start Manual Craft"}
                </button>

                {/* Quick Solve button */}
                <button
                  onClick={handleQuickSolve}
                  disabled={simulator.isLoading || solver.isSolving || solveBlockedReason !== undefined}
                  title={solveBlockedReason}
                  aria-describedby={solveBlock?.describedBy}
                  className="flex flex-col items-center justify-center gap-0.5 py-3 px-6 bg-bg-surface border border-border text-text-primary font-bold rounded-lg hover:border-gold hover:text-gold disabled:opacity-50 disabled:cursor-not-allowed transition-colors text-base"
                >
                  <div className="flex items-center gap-2">
                    <Zap size={18} />
                    Quick Solve
                  </div>
                  <span className="text-xs font-normal text-text-muted">
                    Instant greedy result
                  </span>
                </button>

                {/* Auto-Solve button */}
                <SolverButton
                  isSetupComplete={simulator.isSetupComplete}
                  isSimulating={false}
                  solverStatus={solver.solverStatus}
                  solverResult={solver.solverResult}
                  quickResult={solver.quickResult}
                  elapsedMs={solver.elapsedMs}
                  progressPercent={solver.progressPercent}
                  error={solver.error}
                  isSolving={solver.isSolving}
                  onStartSolve={handleAutoSolve}
                  onCancelSolve={handleCancelSolve}
                  onViewRotation={handleViewRotation}
                  onReSolve={handleAutoSolve}
                  onClearResult={solver.clearResult}
                  onLoadPreview={handleLoadPreview}
                  configSummary={solverConfig.summaryText}
                  disabledReason={
                    !simulator.selectedRecipe
                      ? "Select a recipe first"
                      : !simulator.crafterStats
                        ? "Enter crafter stats first"
                        : solveBlockedReason
                  }
                />

                {/* What If button */}
                <button
                  onClick={() =>
                    setSetupMode(setupMode === "whatif" ? "craft" : "whatif")
                  }
                  className={`flex items-center justify-center gap-2 py-3 px-6 border font-bold rounded-lg transition-colors text-base ${
                    setupMode === "whatif"
                      ? "bg-gold/10 text-gold border-gold/50"
                      : "bg-bg-surface border-border text-text-primary hover:border-gold hover:text-gold"
                  }`}
                >
                  <SlidersHorizontal size={18} />
                  What If
                </button>
              </div>

              {/* Solver config panel */}
              <SolverConfigPanel
                config={solverConfig.config}
                onAlgorithmChange={solverConfig.setAlgorithm}
                onTimeBudgetChange={solverConfig.setTimeBudget}
                onObjectiveChange={solverConfig.setObjective}
                onTargetQualityChange={solverConfig.setTargetQuality}
                onAllowUnsafeChange={solverConfig.setAllowUnsafe}
                onResetDefaults={solverConfig.resetToDefaults}
                summaryText={solverConfig.summaryText}
                disabled={solver.isSolving}
              />

              {/* What If panel */}
              {setupMode === "whatif" && (
                <WhatIfPanel
                  whatIf={whatIf}
                  recipeName={fullRecipe?.resultItem?.name}
                  recipeLevel={fullRecipe?.recipeLevel}
                  craftType={fullRecipe?.craftType}
                />
              )}
            </div>
          )}

          {/* Macro export panel (for solver result context) */}
          {showRotationResult && (
            <MacroExportPanel
              isOpen={showMacroPanel}
              actionIds={
                (solver.solverResult ?? solver.quickResult)?.actionIds ?? []
              }
              stateHistory={
                (
                  solver.solverResult?.simulation ??
                  solver.quickResult?.simulation
                )?.steps ?? []
              }
              currentState={
                (
                  solver.solverResult?.simulation ??
                  solver.quickResult?.simulation
                )?.finalState ?? null
              }
              onClose={() => setShowMacroPanel(false)}
            />
          )}

          {/* Rotation replay overlay */}
          {showReplay &&
            (solver.solverResult?.simulation ??
              solver.quickResult?.simulation) && (
              <RotationReplay
                simulation={
                  (solver.solverResult?.simulation ??
                    solver.quickResult?.simulation)!
                }
                initialStepIndex={replayInitialStep}
                onClose={handleCloseReplay}
                onLoadIntoSimulator={handleLoadIntoSimulator}
              />
            )}
        </div>
      )}
    </div>
  );
}

export default function SimulatorPage() {
  return (
    <Suspense
      fallback={
        <div className="p-4">
          <div className="max-w-4xl mx-auto">
            <div className="flex items-center gap-2 mb-6">
              <FlaskConical className="text-gold" size={24} />
              <h1 className="text-xl font-bold">Crafting Simulator</h1>
            </div>
            <LoadingSkeleton count={3} />
          </div>
        </div>
      }
    >
      <SimulatorPageContent />
    </Suspense>
  );
}
