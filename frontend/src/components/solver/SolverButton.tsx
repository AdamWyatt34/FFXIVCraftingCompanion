"use client";

import { useEffect, useId, useState } from "react";
import {
  Sparkles,
  CheckCircle,
  XCircle,
  Loader2,
  Eye,
  RotateCcw,
} from "lucide-react";
import type { SolverJobStatus, SolverResult } from "@/types/solver";

interface SolverButtonProps {
  isSetupComplete: boolean;
  isSimulating: boolean;
  solverStatus: SolverJobStatus | "Idle";
  solverResult: SolverResult | null;
  quickResult: SolverResult | null;
  elapsedMs: number;
  progressPercent: number;
  error: string | null;
  isSolving: boolean;
  onStartSolve: () => void;
  onCancelSolve: () => void;
  onViewRotation: () => void;
  onReSolve: () => void;
  onClearResult: () => void;
  onLoadPreview: () => void;
  configSummary: string;
  /** Why solving is unavailable; any reason disables the button. */
  disabledReason?: string;
}

function formatElapsed(ms: number): string {
  return `${(ms / 1000).toFixed(1)}s`;
}

export function SolverButton({
  isSetupComplete,
  solverStatus,
  solverResult,
  quickResult,
  elapsedMs,
  progressPercent,
  error,
  isSolving,
  onStartSolve,
  onCancelSolve,
  onViewRotation,
  onReSolve,
  onClearResult,
  onLoadPreview,
  configSummary,
  disabledReason,
}: SolverButtonProps) {
  // Local elapsed timer for smoother updates
  const [displayElapsed, setDisplayElapsed] = useState(0);
  const reasonId = useId();

  useEffect(() => {
    if (!isSolving) {
      const id = requestAnimationFrame(() => setDisplayElapsed(elapsedMs));
      return () => cancelAnimationFrame(id);
    }

    const frameId = requestAnimationFrame(() => setDisplayElapsed(elapsedMs));
    const start = Date.now() - elapsedMs;
    const interval = setInterval(() => {
      setDisplayElapsed(Date.now() - start);
    }, 500);

    return () => {
      cancelAnimationFrame(frameId);
      clearInterval(interval);
    };
  }, [isSolving, elapsedMs]);

  // Failed / TimedOut state
  if (solverStatus === "Failed" || solverStatus === "TimedOut") {
    return (
      <div
        className="rounded-lg border border-red-500/30 bg-red-500/5 p-4 animate-fade-in"
        role="status"
        aria-live="polite"
      >
        <div className="flex items-center gap-2 mb-2">
          <XCircle size={18} className="text-red-400 shrink-0" />
          <span className="font-semibold text-red-400">
            {solverStatus === "TimedOut" ? "Solve Timed Out" : "Solve Failed"}
          </span>
        </div>
        {error && (
          <p className="text-sm text-text-secondary mb-3">{error}</p>
        )}
        <div className="flex items-center gap-2">
          <button
            onClick={onReSolve}
            className="flex items-center gap-1.5 px-3 py-1.5 bg-bg-elevated text-text-primary font-medium rounded-lg hover:border-gold border border-border transition-colors text-sm"
          >
            <RotateCcw size={14} />
            Try Again
          </button>
          <button
            onClick={onClearResult}
            className="px-3 py-1.5 text-text-secondary hover:text-text-primary transition-colors text-sm"
          >
            Dismiss
          </button>
        </div>
      </div>
    );
  }

  // Completed state
  if (solverStatus === "Completed" && (solverResult || quickResult)) {
    const result = solverResult ?? quickResult!;
    const metrics = result.metrics;

    return (
      <div
        className="rounded-lg border border-emerald-500/30 bg-emerald-500/5 p-4 animate-fade-in"
        role="status"
        aria-live="polite"
      >
        <div className="flex items-center gap-2 mb-2">
          <CheckCircle size={18} className="text-emerald-400 shrink-0" />
          <span className="font-semibold text-emerald-400">Rotation found</span>
          {metrics.solveTimeMs > 0 && (
            <span className="text-xs text-text-muted ml-auto">
              {formatElapsed(metrics.solveTimeMs)}
            </span>
          )}
        </div>

        <div className="grid grid-cols-1 sm:grid-cols-3 gap-2 text-center mb-3">
          <div>
            <div className="text-lg font-bold text-text-primary">
              {metrics.finalHQPercent}%
            </div>
            <div className="text-xs text-text-muted">HQ</div>
          </div>
          <div>
            <div className="text-lg font-bold text-text-primary">
              {metrics.totalSteps}
            </div>
            <div className="text-xs text-text-muted">Steps</div>
          </div>
          <div>
            <div className="text-lg font-bold text-text-primary">
              {metrics.algorithmUsed}
            </div>
            <div className="text-xs text-text-muted">Algorithm</div>
          </div>
        </div>

        <div className="flex items-center gap-2">
          <button
            onClick={onViewRotation}
            className="flex-1 flex items-center justify-center gap-1.5 py-2 px-4 bg-gold text-bg-primary font-bold rounded-lg hover:bg-gold-light transition-colors text-sm"
          >
            <Eye size={14} />
            View Rotation
          </button>
          <button
            onClick={onReSolve}
            className="flex items-center gap-1.5 px-3 py-2 bg-bg-elevated text-text-secondary font-medium rounded-lg hover:text-text-primary border border-border transition-colors text-sm"
            aria-label="Re-solve with current settings"
          >
            <RotateCcw size={14} />
          </button>
        </div>
      </div>
    );
  }

  // Solving state (Queued or Running)
  if (isSolving) {
    const hasQuickResult = quickResult !== null;

    return (
      <div
        className="rounded-lg border border-gold/30 bg-bg-surface p-4 animate-fade-in"
        aria-busy="true"
        role="status"
      >
        <div className="flex items-center gap-2 mb-2">
          <Loader2 size={18} className="text-gold animate-spin shrink-0" />
          <span className="font-semibold text-text-primary">
            {hasQuickResult ? "Optimizing..." : "Solving..."}
          </span>
          <span className="text-sm text-text-muted ml-auto tabular-nums" aria-live="polite">
            {formatElapsed(displayElapsed)}
          </span>
        </div>

        {/* Progress bar */}
        <div
          className="w-full h-1.5 bg-bg-elevated rounded-full overflow-hidden mb-2"
          role="progressbar"
          aria-valuenow={Math.round(progressPercent)}
          aria-valuemin={0}
          aria-valuemax={100}
          aria-label={`Solve progress: ${Math.round(progressPercent)}%`}
        >
          <div
            className="h-full bg-gold rounded-full transition-all duration-500 ease-out animate-progress-fill"
            style={{ width: `${progressPercent}%` }}
          />
        </div>

        {/* Quick result preview */}
        {hasQuickResult && (
          <div className="mt-3 p-2 rounded border border-border bg-bg-elevated animate-slide-up">
            <div className="flex items-center justify-between">
              <div className="text-sm">
                <span className="text-text-secondary">Preview: </span>
                <span className="text-text-primary font-medium">
                  {quickResult.metrics.totalSteps} steps · {quickResult.metrics.finalHQPercent}% HQ
                </span>
              </div>
              <button
                onClick={onLoadPreview}
                className="text-xs text-gold hover:text-gold-light font-medium transition-colors"
              >
                Load Preview
              </button>
            </div>
          </div>
        )}

        <button
          onClick={onCancelSolve}
          className="mt-2 w-full text-center text-sm text-text-secondary hover:text-text-primary transition-colors"
        >
          Cancel
        </button>
      </div>
    );
  }

  // Idle state — primary trigger button
  const disabled = !isSetupComplete || disabledReason !== undefined;
  const showReason = disabled && disabledReason !== undefined;

  const button = (
    <button
      onClick={onStartSolve}
      disabled={disabled}
      className={`w-full py-3 px-6 rounded-lg font-bold text-lg transition-all ${
        disabled
          ? "opacity-50 cursor-not-allowed bg-bg-elevated text-text-muted border border-border"
          : "bg-gradient-to-r from-[#b8901b] via-[#e6b422] to-[#f0cc5b] text-bg-primary hover:shadow-lg hover:shadow-[#e6b422]/20 animate-gold-shimmer"
      }`}
      aria-label="Auto-solve crafting rotation"
      aria-disabled={disabled}
      aria-describedby={showReason ? reasonId : undefined}
    >
      <div className="flex items-center justify-center gap-2">
        <Sparkles size={20} />
        <span>Auto-Solve</span>
      </div>
      <div className={`text-xs mt-0.5 ${disabled ? "text-text-muted" : "text-bg-primary/70"}`}>
        {configSummary}
      </div>
    </button>
  );

  if (showReason) {
    return (
      <div>
        <span title={disabledReason} className="block">
          {button}
        </span>
        <p id={reasonId} className="text-xs text-text-muted mt-1">
          {disabledReason}
        </p>
      </div>
    );
  }

  return button;
}
