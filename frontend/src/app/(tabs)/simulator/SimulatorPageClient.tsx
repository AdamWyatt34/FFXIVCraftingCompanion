"use client";

import { useState, useCallback, useEffect, useRef, Suspense } from "react";
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
import { useSimulator } from "@/hooks/useSimulator";
import { useSolver } from "@/hooks/useSolver";
import { useSolverConfig } from "@/hooks/useSolverConfig";
import { useWhatIf } from "@/hooks/useWhatIf";
import { submitRotation } from "@/lib/api-client";
import { getRecipe } from "@/lib/game-data";
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
import { buildSolverShareUrl, parseSolverShareUrl } from "@/lib/rotation-codec";

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
import type { CrafterStatsInput, HQIngredientInput } from "@/types/simulator";

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
  const simulator = useSimulator();
  const solver = useSolver();
  const solverConfig = useSolverConfig();
  const whatIf = useWhatIf();
  const { user } = useAuth();
  const searchParams = useSearchParams();
  const [fullRecipe, setFullRecipe] = useState<RecipeDto | null>(null);
  const [hqIngredients, setHqIngredients] = useState<HQIngredientInput[]>([]);
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
  const [isSavingToLibrary, setIsSavingToLibrary] = useState(false);
  const [saveToLibrarySuccess, setSaveToLibrarySuccess] = useState(false);
  const [isSavingManual, setIsSavingManual] = useState(false);
  const [saveManualSuccess, setSaveManualSuccess] = useState(false);
  const [setupMode, setSetupMode] = useState<"craft" | "solve" | "whatif">(
    "craft",
  );
  const hasInitializedFromUrlRef = useRef(false);
  const autoSolveTriggeredRef = useRef(false);
  const { toast } = useToast();

  // URL-driven initialization
  useEffect(() => {
    if (hasInitializedFromUrlRef.current) return;

    // A solver share link (?solve=1&r=...) carries the recipe, the stats and the packed rotation.
    const shared = parseSolverShareUrl(searchParams);
    if (shared) {
      hasInitializedFromUrlRef.current = true;
      simulator.loadSharedRotation(shared.recipeId, shared.stats, shared.actionIds);
      setShowRotationResult(true);
      return;
    }

    const recipeIdParam = searchParams.get("recipeId");
    const craftsmanshipParam = searchParams.get("craftsmanship");
    const controlParam = searchParams.get("control");
    const cpParam = searchParams.get("cp");
    const levelParam = searchParams.get("level");
    const rotationParam = searchParams.get("rotation");

    const hasUrlParams = recipeIdParam || craftsmanshipParam;
    if (!hasUrlParams) return;

    hasInitializedFromUrlRef.current = true;

    const recipeId = recipeIdParam ? Number(recipeIdParam) : null;
    const hasStats =
      craftsmanshipParam && controlParam && cpParam && levelParam;

    let stats: CrafterStatsInput | null = hasStats
      ? {
          level: Number(levelParam),
          craftsmanship: Number(craftsmanshipParam),
          control: Number(controlParam),
          cp: Number(cpParam),
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
    const compareParam = searchParams.get("compare") === "true";

    // If we have all three (recipe + stats + rotation), load as shared rotation
    if (recipeId && stats && rotationIds) {
      if (compareParam) {
        // Compare mode: show rotation result panel without entering craft view
        simulator.loadSharedRotation(recipeId, stats, rotationIds);
        setShowRotationResult(true);
      } else {
        simulator.loadSharedRotation(recipeId, stats, rotationIds);
      }
      return;
    }

    // Otherwise, auto-populate what we have
    if (recipeId) {
      getRecipe(recipeId).then((recipe) => {
        setFullRecipe(recipe);
        simulator.setRecipe(recipeId);
      });
    }

    if (stats) {
      simulator.setStatsFromUrl(stats);
    }

    if (whatifParam) {
      setSetupMode("whatif");
    } else if (solveParam) {
      setSetupMode("solve");
    }
  }, [searchParams, simulator, toast]);

  // Sync whatIf hook with simulator state
  useEffect(() => {
    if (simulator.crafterStats && simulator.selectedRecipe) {
      whatIf.setBaseStats({
        craftsmanship: simulator.crafterStats.craftsmanship,
        control: simulator.crafterStats.control,
        cp: simulator.crafterStats.cp,
        level: simulator.crafterStats.level,
      });
      whatIf.setRecipeId(simulator.selectedRecipe.recipeId);
    }
  }, [simulator.crafterStats, simulator.selectedRecipe, whatIf]);

  // Navigation warning during active simulation
  useEffect(() => {
    if (!simulator.isSimulating || simulator.stateHistory.length === 0) return;
    const handler = (e: BeforeUnloadEvent) => {
      e.preventDefault();
    };
    window.addEventListener("beforeunload", handler);
    return () => window.removeEventListener("beforeunload", handler);
  }, [simulator.isSimulating, simulator.stateHistory.length]);

  const handleRecipeSelected = useCallback(
    async (recipe: RecipeDto) => {
      setFullRecipe(recipe);
      await simulator.setRecipe(recipe.recipeId);
    },
    [simulator],
  );

  const handleRecipeCleared = useCallback(() => {
    setFullRecipe(null);
    setHqIngredients([]);
  }, []);

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
    const recipeId =
      searchParams.get("recipeId") ?? simulator.selectedRecipe?.recipeId;
    if (!recipeId || !simulator.crafterStats) return;

    const url = buildShareUrl({
      recipeId: Number(recipeId),
      stats: simulator.crafterStats,
      actionIds: simulator.actionIds,
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
    searchParams,
    simulator.selectedRecipe,
    simulator.crafterStats,
    simulator.actionIds,
  ]);

  // ── Solver handlers ──

  const handleAutoSolve = useCallback(async () => {
    if (!simulator.selectedRecipe || !simulator.crafterStats) return;
    const request = solverConfig.buildSolveRequest(
      simulator.selectedRecipe.recipeId,
      simulator.crafterStats,
      hqIngredients.length > 0 ? hqIngredients : undefined,
    );
    await solver.startSolve(request);
  }, [
    simulator.selectedRecipe,
    simulator.crafterStats,
    solverConfig,
    solver,
    hqIngredients,
  ]);

  // Auto-solve trigger from URL param (must be after handleAutoSolve definition)
  useEffect(() => {
    if (autoSolveTriggeredRef.current) return;
    if (setupMode !== "solve") return;
    if (!simulator.isSetupComplete) return;

    autoSolveTriggeredRef.current = true;
    const timer = setTimeout(() => {
      handleAutoSolve();
    }, 100);
    return () => clearTimeout(timer);
  }, [setupMode, simulator.isSetupComplete, handleAutoSolve]);

  const handleQuickSolve = useCallback(async () => {
    if (!simulator.selectedRecipe || !simulator.crafterStats) return;
    await solver.startQuickSolve({
      recipeId: simulator.selectedRecipe.recipeId,
      craftsmanship: simulator.crafterStats.craftsmanship,
      control: simulator.crafterStats.control,
      cp: simulator.crafterStats.cp,
      level: simulator.crafterStats.level,
    });
  }, [simulator.selectedRecipe, simulator.crafterStats, solver]);

  const handleAutoSolveFromCraft = useCallback(async () => {
    if (!simulator.selectedRecipe || !simulator.crafterStats) return;
    setShowSolverOverlay(true);
    const request = solverConfig.buildSolveRequest(
      simulator.selectedRecipe.recipeId,
      simulator.crafterStats,
      hqIngredients.length > 0 ? hqIngredients : undefined,
    );
    await solver.startSolve(request);
  }, [
    simulator.selectedRecipe,
    simulator.crafterStats,
    solverConfig,
    solver,
    hqIngredients,
  ]);

  const handleLoadPreview = useCallback(async () => {
    const result = solver.quickResult;
    if (!result || !simulator.selectedRecipe || !simulator.crafterStats) return;
    solver.cancelSolve();
    setShowSolverOverlay(false);
    await simulator.loadSharedRotation(
      simulator.selectedRecipe.recipeId,
      simulator.crafterStats,
      result.actionIds,
    );
  }, [solver, simulator]);

  const handleViewRotation = useCallback(() => {
    setShowRotationResult(true);
    setShowSolverOverlay(false);
  }, []);

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
    if (!result || !simulator.selectedRecipe || !simulator.crafterStats) return;
    await simulator.loadSharedRotation(
      simulator.selectedRecipe.recipeId,
      simulator.crafterStats,
      result.actionIds,
    );
    solver.clearResult();
    setShowRotationResult(false);
    setShowReplay(false);
  }, [solver, simulator]);

  const handleSolverShare = useCallback(async () => {
    const result = solver.solverResult ?? solver.quickResult;
    if (!result || !simulator.selectedRecipe || !simulator.crafterStats) return;

    const url = buildSolverShareUrl({
      recipeId: simulator.selectedRecipe.recipeId,
      stats: simulator.crafterStats,
      actionIds: result.actionIds,
      algorithm: result.metrics.algorithmUsed,
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
  }, [solver, simulator.selectedRecipe, simulator.crafterStats]);

  const handleDismissResult = useCallback(() => {
    setShowRotationResult(false);
    solver.clearResult();
  }, [solver]);

  const handleSaveToLibrary = useCallback(async () => {
    const result = solver.solverResult ?? solver.quickResult;
    if (!result || !simulator.selectedRecipe || !simulator.crafterStats) return;
    setIsSavingToLibrary(true);
    try {
      await submitRotation({
        recipeId: simulator.selectedRecipe.recipeId,
        actionIds: result.actionIds,
        stats: simulator.crafterStats,
        source: "Solver",
        algorithm: result.metrics.algorithmUsed,
      });
      setSaveToLibrarySuccess(true);
      setTimeout(() => setSaveToLibrarySuccess(false), 3000);
    } catch {
      // Error is silently handled — could add toast later
    } finally {
      setIsSavingToLibrary(false);
    }
  }, [solver, simulator.selectedRecipe, simulator.crafterStats]);

  const handleSaveManualToLibrary = useCallback(async () => {
    if (
      !simulator.selectedRecipe ||
      !simulator.crafterStats ||
      simulator.actionIds.length === 0
    )
      return;
    setIsSavingManual(true);
    try {
      await submitRotation({
        recipeId: simulator.selectedRecipe.recipeId,
        actionIds: simulator.actionIds,
        stats: simulator.crafterStats,
        source: "Manual",
      });
      setSaveManualSuccess(true);
      setTimeout(() => setSaveManualSuccess(false), 3000);
    } catch {
      // Error is silently handled
    } finally {
      setIsSavingManual(false);
    }
  }, [simulator.selectedRecipe, simulator.crafterStats, simulator.actionIds]);

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
            onBackToSetup={simulator.exitSimulation}
          />

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
                onUndo={simulator.undoLastAction}
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
                  onViewRotation={handleViewRotation}
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
                        className="flex items-center gap-1.5 px-3 py-1.5 bg-bg-elevated text-text-secondary font-medium rounded-lg hover:text-gold hover:border-gold/30 border border-border transition-colors text-sm"
                        aria-label="Quick solve from current state"
                      >
                        <Zap size={14} />
                        Quick Solve
                      </button>
                      <button
                        onClick={handleAutoSolveFromCraft}
                        className="flex items-center gap-1.5 px-3 py-1.5 bg-gold/10 text-gold font-medium rounded-lg hover:bg-gold/20 border border-gold/30 transition-colors text-sm"
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
                  stats={simulator.crafterStats}
                  onStatsChange={handleStatsChange}
                  selectedCraftType={fullRecipe.craftType as CraftType}
                />
              </section>

              {/* Step 3: Recipe Parameters */}
              {hasStats ? (
                <section className="animate-fade-in">
                  <SectionHeader number={3} title="Recipe Parameters" />
                  <RecipeParams
                    recipe={fullRecipe}
                    stats={simulator.crafterStats}
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
                recipe={fullRecipe}
                onChange={setHqIngredients}
              />
            </section>
          )}

          {/* Starting Quality Input */}
          {fullRecipe && hasStats && (
            <section className="animate-slide-up">
              <div className="bg-bg-surface border border-border rounded-lg p-4">
                <label
                  htmlFor="starting-quality"
                  className="block text-sm font-semibold text-text-primary mb-2"
                >
                  Starting Quality
                </label>
                <p className="text-xs text-text-muted mb-3">
                  Quality bonus from using HQ materials. Leave at 0 for no
                  bonus.
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
                recipeId={simulator.selectedRecipe!.recipeId}
                crafterStats={simulator.crafterStats!}
                onReplay={handleOpenReplay}
                onJumpToStep={handleJumpToStep}
                onExportMacro={() => setShowMacroPanel(true)}
                onShare={handleSolverShare}
                onReSolve={handleAutoSolve}
                onLoadIntoSimulator={handleLoadIntoSimulator}
                onDismiss={handleDismissResult}
                shareConfirmation={shareConfirmation}
                onSaveToLibrary={user ? handleSaveToLibrary : undefined}
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
                  disabled={simulator.isLoading || solver.isSolving}
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
                        : undefined
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
