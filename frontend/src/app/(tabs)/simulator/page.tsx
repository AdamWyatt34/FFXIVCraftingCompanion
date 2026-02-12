"use client";

import { useState, useCallback, useEffect, useRef, Suspense } from "react";
import { useSearchParams } from "next/navigation";
import { FlaskConical, Check, X } from "lucide-react";
import { useSimulator } from "@/hooks/useSimulator";
import { getRecipe } from "@/lib/api-client";
import { parseRotationFromUrl } from "@/lib/format-utils";
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
import { MacroExportPanel } from "@/components/simulator/MacroExportPanel";
import { LoadingSkeleton } from "@/components/ui/LoadingStates";
import { buildShareUrl } from "@/lib/format-utils";
import type { RecipeDto, CraftType } from "@/types/api";
import type { CrafterStatsInput } from "@/types/simulator";

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
  const searchParams = useSearchParams();
  const [fullRecipe, setFullRecipe] = useState<RecipeDto | null>(null);
  const [showMacroPanel, setShowMacroPanel] = useState(false);
  const [shareConfirmation, setShareConfirmation] = useState(false);
  const hasInitializedFromUrlRef = useRef(false);

  // URL-driven initialization
  useEffect(() => {
    if (hasInitializedFromUrlRef.current) return;

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

    const stats: CrafterStatsInput | null = hasStats
      ? {
          level: Number(levelParam),
          craftsmanship: Number(craftsmanshipParam),
          control: Number(controlParam),
          cp: Number(cpParam),
        }
      : null;

    const rotationIds = parseRotationFromUrl(rotationParam);

    // If we have all three (recipe + stats + rotation), load as shared rotation
    if (recipeId && stats && rotationIds) {
      simulator.loadSharedRotation(recipeId, stats, rotationIds);
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
  }, [searchParams, simulator]);

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
    const recipeId = searchParams.get("recipeId") ?? simulator.selectedRecipe?.recipeId;
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
  }, [searchParams, simulator.selectedRecipe, simulator.crafterStats, simulator.actionIds]);

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
      <div className="sr-only" aria-live="assertive" aria-atomic="true">
        {srAnnouncement}
      </div>

      {/* Header — hidden during simulation (SimulatorHeader replaces it) */}
      {!simulator.isSimulating && (
        <div className="max-w-4xl mx-auto">
          <div className="flex items-center gap-2 mb-6">
            <FlaskConical className="text-gold" size={24} />
            <h1 className="text-xl font-bold">Crafting Simulator</h1>
          </div>
        </div>
      )}

      {/* Error banner */}
      {simulator.error && (
        <div className={`${simulator.isSimulating ? "max-w-7xl" : "max-w-4xl"} mx-auto mb-4`}>
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
                      innerQuietStacks={
                        simulator.currentState.innerQuietStacks
                      }
                    />
                  </div>
                </div>
                <HQProbabilityDisplay
                  hqPercent={simulator.hqPercent}
                  compact
                />
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
                />
              )}

              {/* Action panel */}
              {simulator.currentState.status === "InProgress" && (
                <ActionPanel
                  actions={simulator.actions}
                  availableActions={simulator.availableActions}
                  isLoading={simulator.isLoading}
                  onExecute={simulator.executeAction}
                  onUndo={simulator.undoLastAction}
                  onReset={simulator.resetCraft}
                />
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

          {/* Start Crafting button */}
          {simulator.isSetupComplete && (
            <div className="animate-slide-up">
              <button
                onClick={handleStartCrafting}
                disabled={simulator.isLoading}
                className="w-full py-3 px-6 bg-gold text-bg-primary font-bold rounded-lg hover:bg-gold-light disabled:opacity-50 disabled:cursor-not-allowed transition-colors text-lg"
              >
                {simulator.isLoading ? "Initializing..." : "Start Crafting"}
              </button>
            </div>
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
