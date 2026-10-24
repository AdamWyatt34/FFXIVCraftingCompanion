"use client";

import { useId, useMemo } from "react";
import Image from "next/image";
import {
  Play,
  FileText,
  Link2,
  RotateCcw,
  Wrench,
  X,
  CheckCircle,
  Check,
  BookOpen,
} from "lucide-react";
import { getActionIconUrl, ACTION_CATEGORY_COLORS } from "@/lib/action-icons";
import { HQProbabilityDisplay } from "@/components/simulator/HQProbabilityDisplay";
import { SolverMetrics } from "./SolverMetrics";
import type { SolverResult } from "@/types/solver";
import type { SimulationResult, ActionCategory } from "@/types/simulator";

interface RotationResultPanelProps {
  solverResult: SolverResult;
  quickResult: SolverResult | null;
  simulation: SimulationResult;
  onReplay: () => void;
  onJumpToStep: (stepIndex: number) => void;
  onExportMacro: () => void;
  onShare: () => void;
  onReSolve: () => void;
  onLoadIntoSimulator: () => void;
  onDismiss: () => void;
  shareConfirmation: boolean;
  onSaveToLibrary?: () => void;
  /** Why the result cannot be saved; any reason disables Save. */
  saveDisabledReason?: string;
  isSaving?: boolean;
  saveSuccess?: boolean;
}

interface CollapsedGroup {
  actionName: string;
  actionCategory: string;
  count: number;
  startIndex: number; // 1-based step index for replay jump
  cpCost: number;
}

function collapseSteps(simulation: SimulationResult): CollapsedGroup[] {
  const groups: CollapsedGroup[] = [];
  for (let i = 0; i < simulation.steps.length; i++) {
    const step = simulation.steps[i];
    const last = groups[groups.length - 1];
    if (last && last.actionName === step.actionName) {
      last.count++;
    } else {
      groups.push({
        actionName: step.actionName,
        actionCategory: step.actionCategory,
        count: 1,
        startIndex: i + 1, // 1-based for replay
        cpCost: Math.abs(step.deltas.cp),
      });
    }
  }
  return groups;
}

function getQualityColor(pct: number): string {
  if (pct >= 90) return "text-emerald-400";
  if (pct >= 60) return "text-amber-400";
  return "text-red-400";
}

function formatMacroTime(steps: number): string {
  const totalSeconds = steps * 3;
  if (totalSeconds < 60) return `~${totalSeconds}s`;
  const mins = Math.floor(totalSeconds / 60);
  const secs = totalSeconds % 60;
  return secs > 0 ? `~${mins}m ${secs}s` : `~${mins}m`;
}

export function RotationResultPanel({
  solverResult,
  quickResult,
  simulation,
  onReplay,
  onJumpToStep,
  onExportMacro,
  onShare,
  onReSolve,
  onLoadIntoSimulator,
  onDismiss,
  shareConfirmation,
  onSaveToLibrary,
  saveDisabledReason,
  isSaving,
  saveSuccess,
}: RotationResultPanelProps) {
  const saveReasonId = useId();
  const { metrics } = solverResult;
  const { finalState } = simulation;

  const groups = useMemo(() => collapseSteps(simulation), [simulation]);

  const improvement =
    quickResult &&
    metrics.finalQualityPercent > quickResult.metrics.finalQualityPercent
      ? metrics.finalQualityPercent - quickResult.metrics.finalQualityPercent
      : null;

  return (
    <div
      className="rounded-lg border border-emerald-500/30 bg-emerald-500/5 p-4 sm:p-5 space-y-4 animate-fade-in"
      role="region"
      aria-label="Solver result"
    >
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-2">
          <CheckCircle size={20} className="text-emerald-400" />
          <h3 className="text-lg font-bold text-emerald-400">
            Rotation found
          </h3>
        </div>
        <button
          onClick={onDismiss}
          className="text-text-muted hover:text-text-primary transition-colors p-1"
          aria-label="Dismiss result"
        >
          <X size={18} />
        </button>
      </div>

      {/* Summary grid */}
      <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
        {/* Quality */}
        <div className="bg-bg-elevated rounded-md p-3 text-center">
          <div
            className={`text-lg font-bold ${getQualityColor(finalState.qualityPercent)}`}
          >
            {Math.round(finalState.qualityPercent)}%
          </div>
          <div className="text-xs text-text-muted">
            {finalState.quality.toLocaleString()} /{" "}
            {finalState.maxQuality.toLocaleString()}
          </div>
          <div className="text-xs text-text-muted mt-0.5">Quality</div>
        </div>

        {/* HQ */}
        <div className="bg-bg-elevated rounded-md p-3 flex flex-col items-center justify-center">
          <HQProbabilityDisplay hqPercent={metrics.finalHQPercent} compact />
        </div>

        {/* Steps + Duration */}
        <div className="bg-bg-elevated rounded-md p-3 text-center">
          <div className="text-lg font-bold text-text-primary">
            {metrics.totalSteps}
          </div>
          <div className="text-xs text-text-muted">Steps</div>
          <div className="text-xs text-text-muted mt-0.5">
            {formatMacroTime(metrics.totalSteps)} macro time
          </div>
        </div>

        {/* Algorithm + Time */}
        <div className="bg-bg-elevated rounded-md p-3 text-center">
          <div className="text-lg font-bold text-text-primary">
            {metrics.algorithmUsed}
          </div>
          <div className="text-xs text-text-muted">Algorithm</div>
          <div className="text-xs text-text-muted mt-0.5">
            {(metrics.solveTimeMs / 1000).toFixed(1)}s solve time
          </div>
        </div>
      </div>

      {metrics.finalHQPercent < 50 && (
        <p className="text-sm">
          <a
            href="#consumables"
            className="text-amber-400 underline underline-offset-2 hover:text-amber-300 transition-colors"
          >
            Raise HQ with food, medicine or HQ materials.
          </a>
        </p>
      )}

      {/* Improvement indicator */}
      {improvement !== null && improvement > 0 && (
        <div className="text-sm text-emerald-400 flex items-center gap-1">
          <span>+{Math.round(improvement)}% quality over instant solver</span>
        </div>
      )}

      {/* Rotation strip */}
      <div>
        <div className="text-xs font-medium text-text-muted mb-1.5">
          Rotation
        </div>
        <div
          className="flex flex-wrap gap-1.5"
          role="list"
          aria-label="Rotation actions"
        >
          {groups.map((group, i) => {
            const color =
              ACTION_CATEGORY_COLORS[group.actionCategory as ActionCategory] ??
              ACTION_CATEGORY_COLORS.Other;
            const iconUrl = getActionIconUrl(group.actionName);
            const label =
              group.count > 1
                ? `${group.actionName} x${group.count}`
                : group.actionName;
            const tooltip = `${group.actionName}${group.count > 1 ? ` x${group.count}` : ""}${group.cpCost > 0 ? ` (${group.cpCost} CP)` : ""}`;

            return (
              <span key={`${group.actionName}-${i}`} title={tooltip}>
                <button
                  onClick={() => onJumpToStep(group.startIndex)}
                  className="flex items-center gap-1 px-2 py-1 rounded text-xs font-medium transition-colors hover:brightness-125 cursor-pointer"
                  style={{
                    backgroundColor: `${color}15`,
                    borderLeft: `3px solid ${color}`,
                    color,
                  }}
                  role="listitem"
                  aria-label={`${label}, click to replay from this step`}
                >
                  <Image
                    src={iconUrl}
                    alt=""
                    width={16}
                    height={16}
                    className="rounded-sm"
                    unoptimized
                  />
                  <span>{label}</span>
                </button>
              </span>
            );
          })}
        </div>
      </div>

      {/* Action buttons */}
      <div className="flex flex-wrap gap-2">
        <button
          onClick={onReplay}
          className="flex items-center gap-1.5 px-4 py-2 bg-gold text-bg-primary font-bold rounded-lg hover:bg-gold-light transition-colors text-sm"
        >
          <Play size={16} />
          Replay
        </button>
        <button
          onClick={onExportMacro}
          className="flex items-center gap-1.5 px-4 py-2 bg-bg-elevated text-text-secondary font-medium rounded-lg hover:text-text-primary border border-border transition-colors text-sm"
        >
          <FileText size={16} />
          Export Macro
        </button>
        <button
          onClick={onShare}
          className="flex items-center gap-1.5 px-4 py-2 bg-bg-elevated text-text-secondary font-medium rounded-lg hover:text-text-primary border border-border transition-colors text-sm"
        >
          {shareConfirmation ? (
            <>
              <Check size={16} className="text-emerald-400" />
              <span className="text-emerald-400">Copied!</span>
            </>
          ) : (
            <>
              <Link2 size={16} />
              Share
            </>
          )}
        </button>
        <button
          onClick={onReSolve}
          className="flex items-center gap-1.5 px-4 py-2 bg-bg-elevated text-text-secondary font-medium rounded-lg hover:text-text-primary border border-border transition-colors text-sm"
        >
          <RotateCcw size={16} />
          Re-Solve
        </button>
        <button
          onClick={onLoadIntoSimulator}
          className="flex items-center gap-1.5 px-4 py-2 bg-bg-elevated text-text-secondary font-medium rounded-lg hover:text-text-primary border border-border transition-colors text-sm"
        >
          <Wrench size={16} />
          Load into Simulator
        </button>
        {onSaveToLibrary && (
          <button
            onClick={onSaveToLibrary}
            disabled={isSaving || saveSuccess || saveDisabledReason !== undefined}
            title={saveDisabledReason}
            aria-describedby={saveDisabledReason !== undefined ? saveReasonId : undefined}
            className={`flex items-center gap-1.5 px-4 py-2 font-medium rounded-lg border transition-colors text-sm ${
              saveSuccess
                ? "bg-emerald-500/10 border-emerald-500/30 text-emerald-400"
                : "bg-bg-elevated text-text-secondary border-border hover:text-text-primary"
            } disabled:opacity-50 disabled:cursor-not-allowed`}
          >
            {saveSuccess ? (
              <>
                <Check size={16} />
                Saved!
              </>
            ) : isSaving ? (
              <>
                <BookOpen size={16} />
                Saving...
              </>
            ) : (
              <>
                <BookOpen size={16} />
                Save to Library
              </>
            )}
          </button>
        )}
      </div>
      {onSaveToLibrary && saveDisabledReason !== undefined && (
        <p id={saveReasonId} className="text-xs text-text-muted">
          {saveDisabledReason}
        </p>
      )}

      {/* Solver metrics (collapsible) */}
      <SolverMetrics
        metrics={metrics}
        quickMetrics={quickResult?.metrics}
      />
    </div>
  );
}
