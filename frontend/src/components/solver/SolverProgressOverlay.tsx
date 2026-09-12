"use client";

import { useEffect, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { Loader2, Eye, X } from "lucide-react";
import type { SolverJobStatus, SolverResult } from "@/types/solver";
import type { SolverProgress } from "@/types/solver-worker";

interface SolverProgressOverlayProps {
  solverStatus: SolverJobStatus | "Idle";
  solverResult: SolverResult | null;
  quickResult: SolverResult | null;
  elapsedMs: number;
  progressPercent: number;
  /** Live search statistics from the local solver; absent on the API path. */
  progress?: SolverProgress | null;
  error: string | null;
  onLoadPreview: () => void;
  onCancelSolve: () => void;
}

function formatElapsed(ms: number): string {
  return `${(ms / 1000).toFixed(1)}s`;
}

function OverlayContent({
  solverStatus,
  quickResult,
  elapsedMs,
  progressPercent,
  progress,
  error,
  onLoadPreview,
  onCancelSolve,
}: Omit<SolverProgressOverlayProps, "solverResult">) {
  const bestHq = progress?.bestHQPercent ?? quickResult?.metrics.finalHQPercent;
  const cancelRef = useRef<HTMLButtonElement>(null);

  // Local elapsed timer for smoother updates
  const [displayElapsed, setDisplayElapsed] = useState(elapsedMs);
  const isSolving = solverStatus === "Queued" || solverStatus === "Running";

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

  // Auto-focus cancel button on mount
  useEffect(() => {
    cancelRef.current?.focus();
  }, []);

  return (
    <div
      className="text-center space-y-4"
      role="dialog"
      aria-label="Solver progress"
    >
      {/* Spinner */}
      <Loader2 size={32} className="text-gold animate-spin mx-auto" />

      {/* Heading */}
      <h3 className="text-lg font-semibold text-text-primary">
        Finding optimal rotation...
      </h3>

      {/* Progress bar */}
      <div>
        <div
          className="w-full h-2 bg-bg-elevated rounded-full overflow-hidden"
          role="progressbar"
          aria-valuenow={Math.round(progressPercent)}
          aria-valuemin={0}
          aria-valuemax={100}
          aria-label={`Solve progress: ${Math.round(progressPercent)}%`}
        >
          <div
            className="h-full bg-gold rounded-full transition-all duration-500 ease-out"
            style={{ width: `${progressPercent}%` }}
          />
        </div>
        <div
          className="text-sm text-text-muted mt-1.5 tabular-nums"
          aria-live="polite"
        >
          {formatElapsed(displayElapsed)} elapsed
          {progress?.iterations !== undefined &&
            ` · ${progress.iterations.toLocaleString()} iterations`}
        </div>
      </div>

      {/* Live stats grid */}
      <div className="grid grid-cols-2 gap-3 text-sm" aria-live="polite">
        <div className="bg-bg-elevated rounded-lg p-2">
          <div className="text-text-muted text-xs">Algorithm</div>
          <div className="text-text-primary font-medium">
            {progress?.algorithm ??
              quickResult?.metrics.algorithmUsed ??
              solverStatus}
          </div>
        </div>
        <div className="bg-bg-elevated rounded-lg p-2">
          <div className="text-text-muted text-xs">Best Quality</div>
          <div className="text-text-primary font-medium">
            {bestHq !== undefined ? `${bestHq}% HQ` : "--"}
          </div>
        </div>
      </div>

      {/* Quick preview section */}
      {quickResult && (
        <div className="border border-border rounded-lg p-3 bg-bg-elevated animate-slide-up">
          <div className="text-sm text-text-secondary mb-2">
            Quick Preview Available
          </div>
          <div className="text-sm text-text-primary font-medium mb-2">
            {quickResult.metrics.totalSteps} steps ·{" "}
            {quickResult.metrics.finalHQPercent}% HQ
          </div>
          <button
            onClick={onLoadPreview}
            className="flex items-center justify-center gap-1.5 w-full py-1.5 text-sm text-gold hover:text-gold-light border border-gold/30 rounded-lg hover:bg-gold/5 transition-colors"
          >
            <Eye size={14} />
            Load Preview
          </button>
        </div>
      )}

      {/* Error display */}
      {error && <p className="text-sm text-red-400">{error}</p>}

      {/* Cancel button */}
      <button
        ref={cancelRef}
        onClick={onCancelSolve}
        className="flex items-center justify-center gap-1.5 mx-auto px-4 py-2 text-sm text-text-secondary hover:text-text-primary border border-border rounded-lg hover:border-border-focus transition-colors"
      >
        <X size={14} />
        Cancel Solve
      </button>
    </div>
  );
}

export function SolverProgressOverlay(props: SolverProgressOverlayProps) {
  const [isMobile, setIsMobile] = useState(false);

  useEffect(() => {
    const check = () => setIsMobile(window.innerWidth < 1024);
    check();
    window.addEventListener("resize", check);
    return () => window.removeEventListener("resize", check);
  }, []);

  // Mobile: full-screen modal via portal
  if (isMobile) {
    return createPortal(
      <div className="fixed inset-0 z-[70] animate-fade-in">
        {/* Backdrop */}
        <div className="absolute inset-0 bg-black/60" />
        {/* Content */}
        <div className="relative flex items-center justify-center min-h-full p-6">
          <div className="bg-bg-surface border border-border rounded-xl p-6 max-w-sm w-full">
            <OverlayContent {...props} />
          </div>
        </div>
      </div>,
      document.body,
    );
  }

  // Desktop: inline overlay over action grid
  return (
    <div className="absolute inset-0 bg-black/50 backdrop-blur-sm rounded-lg flex items-center justify-center z-10 animate-fade-in">
      <div className="bg-bg-surface border border-border rounded-xl p-6 max-w-sm w-full mx-4">
        <OverlayContent {...props} />
      </div>
    </div>
  );
}
