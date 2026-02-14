"use client";

import { useState } from "react";
import { ChevronDown, ChevronUp, TrendingUp } from "lucide-react";
import type { SolverMetrics as SolverMetricsType } from "@/types/solver";

interface SolverMetricsProps {
  metrics: SolverMetricsType;
  quickMetrics?: SolverMetricsType;
  defaultExpanded?: boolean;
}

function formatTime(ms: number): string {
  if (ms < 1000) return `${ms}ms`;
  return `${(ms / 1000).toFixed(1)}s`;
}

function formatNumber(n: number): string {
  if (n >= 1_000_000) return `${(n / 1_000_000).toFixed(1)}M`;
  if (n >= 1_000) return `${(n / 1_000).toFixed(1)}k`;
  return n.toLocaleString();
}

export function SolverMetrics({
  metrics,
  quickMetrics,
  defaultExpanded = false,
}: SolverMetricsProps) {
  const [expanded, setExpanded] = useState(defaultExpanded);

  const improvement =
    quickMetrics &&
    metrics.finalQualityPercent > quickMetrics.finalQualityPercent
      ? metrics.finalQualityPercent - quickMetrics.finalQualityPercent
      : null;

  return (
    <div className="border border-border rounded-lg bg-bg-surface overflow-hidden">
      {/* Toggle header */}
      <button
        onClick={() => setExpanded((v) => !v)}
        className="w-full flex items-center justify-between px-3 py-2 text-sm text-text-secondary hover:text-text-primary transition-colors"
        aria-expanded={expanded}
        aria-controls="solver-metrics-body"
      >
        <div className="flex items-center gap-2">
          <span className="font-medium">Solver Performance</span>
          {!expanded && (
            <span className="text-xs text-text-muted">
              {metrics.algorithmUsed} &middot; {formatTime(metrics.solveTimeMs)}
              {improvement !== null && (
                <span className="text-emerald-400 ml-1">
                  +{Math.round(improvement)}%
                </span>
              )}
            </span>
          )}
        </div>
        {expanded ? <ChevronUp size={16} /> : <ChevronDown size={16} />}
      </button>

      {/* Expandable body */}
      {expanded && (
        <div
          id="solver-metrics-body"
          className="px-3 pb-3 animate-expand-down"
        >
          {/* Stats grid */}
          <div className="grid grid-cols-2 sm:grid-cols-4 gap-2 text-center">
            <div className="bg-bg-elevated rounded-md p-2">
              <div className="text-sm font-semibold text-text-primary">
                {metrics.algorithmUsed}
              </div>
              <div className="text-xs text-text-muted">Algorithm</div>
            </div>
            <div className="bg-bg-elevated rounded-md p-2">
              <div className="text-sm font-semibold text-text-primary">
                {formatTime(metrics.solveTimeMs)}
              </div>
              <div className="text-xs text-text-muted">Solve Time</div>
            </div>
            <div className="bg-bg-elevated rounded-md p-2">
              <div className="text-sm font-semibold text-text-primary">
                {formatNumber(metrics.iterationsCompleted)}
              </div>
              <div className="text-xs text-text-muted">Iterations</div>
            </div>
            <div className="bg-bg-elevated rounded-md p-2">
              <div className="text-sm font-semibold text-text-primary">
                {formatNumber(metrics.nodesExplored)}
              </div>
              <div className="text-xs text-text-muted">Nodes Explored</div>
            </div>
          </div>

          {/* Improvement comparison */}
          {quickMetrics && (
            <div className="mt-2 space-y-1">
              <div className="flex items-center justify-between text-xs">
                <span className="text-text-muted">Instant solver:</span>
                <span className="text-text-secondary">
                  {Math.round(quickMetrics.finalHQPercent)}% HQ &middot;{" "}
                  {quickMetrics.totalSteps} steps
                </span>
              </div>
              <div className="flex items-center justify-between text-xs">
                <span className="text-text-muted">
                  {metrics.algorithmUsed} solver:
                </span>
                <span className="text-text-primary font-medium">
                  {Math.round(metrics.finalHQPercent)}% HQ &middot;{" "}
                  {metrics.totalSteps} steps
                </span>
              </div>
              {improvement !== null && improvement > 0 && (
                <div className="flex items-center gap-1 text-xs text-emerald-400 mt-1">
                  <TrendingUp size={12} />
                  <span>
                    +{Math.round(improvement)}% quality over instant solver
                  </span>
                </div>
              )}
            </div>
          )}
        </div>
      )}
    </div>
  );
}
