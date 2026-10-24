"use client";

import { useEffect, useRef, useSyncExternalStore } from "react";
import type { CraftingState, StepDeltas } from "@/types/simulator";

interface BarSpec {
  label: string;
  shortLabel: string;
  current: number;
  max: number;
  percent: number;
  compactValue: string;
  colorFrom: string;
  colorTo: string;
  warningFrom?: string;
  warningTo?: string;
  warningThreshold?: number;
  delta?: number;
}

function stateBars(state: CraftingState, deltas?: StepDeltas): BarSpec[] {
  const durabilityPercent =
    state.maxDurability > 0
      ? (state.durability / state.maxDurability) * 100
      : 0;
  const cpPercent =
    state.maxCP > 0 ? (state.cp / state.maxCP) * 100 : 0;

  return [
    {
      label: "Progress",
      shortLabel: "Prog",
      current: state.progress,
      max: state.maxProgress,
      percent: state.progressPercent,
      compactValue: `${Math.round(state.progressPercent)}%`,
      colorFrom: "#60a5fa",
      colorTo: "#3b82f6",
      delta: deltas?.progress,
    },
    {
      label: "Quality",
      shortLabel: "Qual",
      current: state.quality,
      max: state.maxQuality,
      percent: state.qualityPercent,
      compactValue: `${Math.round(state.qualityPercent)}%`,
      colorFrom: "#4ade80",
      colorTo: "#22c55e",
      delta: deltas?.quality,
    },
    {
      label: "Durability",
      shortLabel: "Dur",
      current: state.durability,
      max: state.maxDurability,
      percent: durabilityPercent,
      compactValue: state.durability.toLocaleString(),
      colorFrom: "#fbbf24",
      colorTo: "#f59e0b",
      warningFrom: "#ef4444",
      warningTo: "#dc2626",
      warningThreshold: 20,
      delta: deltas?.durability,
    },
    {
      label: "CP",
      shortLabel: "CP",
      current: state.cp,
      max: state.maxCP,
      percent: cpPercent,
      compactValue: state.cp.toLocaleString(),
      colorFrom: "#a78bfa",
      colorTo: "#8b5cf6",
      warningFrom: "#ef4444",
      warningTo: "#dc2626",
      warningThreshold: 15,
      delta: deltas?.cp,
    },
  ];
}

function barBackground(bar: BarSpec): string {
  const isWarning =
    bar.warningThreshold !== undefined &&
    bar.warningThreshold > 0 &&
    bar.percent <= bar.warningThreshold;

  const fromColor = isWarning && bar.warningFrom ? bar.warningFrom : bar.colorFrom;
  const toColor = isWarning && bar.warningTo ? bar.warningTo : bar.colorTo;
  return `linear-gradient(to right, ${fromColor}, ${toColor})`;
}

function ProgressRow({ bar, step }: { bar: BarSpec; step: number }) {
  const { label, shortLabel, current, max, percent, delta } = bar;

  return (
    <div className="space-y-1">
      <div className="flex items-center justify-between text-sm">
        <span className="text-text-secondary font-medium">
          <span className="hidden sm:inline">{label}</span>
          <span className="sm:hidden">{shortLabel}</span>
        </span>
        <div className="flex items-center gap-1.5 sm:gap-2">
          {delta !== undefined && delta !== 0 && (
            <span
              key={`${label}-delta-${step}`}
              className={`text-xs font-medium animate-delta-flash ${
                delta > 0 ? "text-emerald-400" : "text-red-400"
              }`}
            >
              {delta > 0 ? "+" : ""}
              {delta.toLocaleString()}
            </span>
          )}
          <span className="text-text-primary tabular-nums text-xs sm:text-sm">
            {current.toLocaleString()}/{max.toLocaleString()}
          </span>
          <span className="text-text-muted text-xs w-8 sm:w-10 text-right tabular-nums">
            {Math.round(percent)}%
          </span>
        </div>
      </div>
      <div
        className="h-2 sm:h-2.5 bg-bg-elevated rounded-full overflow-hidden"
        role="progressbar"
        aria-label={label}
        aria-valuenow={current}
        aria-valuemin={0}
        aria-valuemax={max}
      >
        <div
          className="h-full rounded-full"
          style={{
            width: `${Math.min(percent, 100)}%`,
            background: barBackground(bar),
            transition: "width 0.3s ease-out, background 0.3s ease",
          }}
        />
      </div>
    </div>
  );
}

// Whether the full bars are on screen: the compact strip shows only once they have scrolled under
// the page header. One full bar is mounted at a time (the craft view or the replay).
let fullBarsInView = true;
const fullBarsListeners = new Set<() => void>();

function setFullBarsInView(inView: boolean) {
  if (fullBarsInView === inView) return;
  fullBarsInView = inView;
  for (const listener of fullBarsListeners) listener();
}

function subscribeToFullBars(listener: () => void) {
  fullBarsListeners.add(listener);
  return () => {
    fullBarsListeners.delete(listener);
  };
}

function CompactStateBar({ state }: { state: CraftingState }) {
  const hidden = useSyncExternalStore(
    subscribeToFullBars,
    () => fullBarsInView,
    () => true,
  );

  // A copy of the full bars, which stay the accessible ones; kept in the layout while hidden so
  // the sticky strip never changes height and shifts the page under it.
  return (
    <div
      aria-hidden="true"
      className={`grid grid-cols-4 gap-2 sm:gap-3 ${hidden ? "invisible" : ""}`}
    >
      {stateBars(state).map((bar) => (
        <div key={bar.label} className="min-w-0">
          <div className="flex items-baseline justify-between gap-1 text-[10px] leading-tight tabular-nums">
            <span className="text-text-muted">{bar.shortLabel}</span>
            <span className="text-text-primary truncate">{bar.compactValue}</span>
          </div>
          <div className="mt-0.5 h-1 bg-bg-elevated rounded-full overflow-hidden">
            <div
              className="h-full rounded-full"
              style={{
                width: `${Math.min(bar.percent, 100)}%`,
                background: barBackground(bar),
                transition: "width 0.3s ease-out, background 0.3s ease",
              }}
            />
          </div>
        </div>
      ))}
    </div>
  );
}

function FullStateBar({
  state,
  previousDeltas,
}: {
  state: CraftingState;
  previousDeltas?: StepDeltas;
}) {
  const rootRef = useRef<HTMLDivElement>(null);
  const [progress, quality, durability, cp] = stateBars(state, previousDeltas);

  useEffect(() => {
    const root = rootRef.current;
    if (!root || typeof IntersectionObserver === "undefined") return;
    // rootMargin takes no rem: 56px is the sticky app header (h-14). Below 95% the bars have
    // started to slide under the strip.
    const observer = new IntersectionObserver(
      ([entry]) => setFullBarsInView(entry.intersectionRatio >= 0.95),
      { rootMargin: "-56px 0px 0px 0px", threshold: 0.95 },
    );
    observer.observe(root);
    return () => {
      observer.disconnect();
      setFullBarsInView(true);
    };
  }, []);

  return (
    <div ref={rootRef} className="bg-bg-surface rounded-lg border border-border p-3 sm:p-4">
      <div className="grid grid-cols-2 gap-2 sm:grid-cols-1 sm:gap-3">
        <div className="space-y-2 sm:space-y-3">
          <ProgressRow bar={progress} step={state.step} />
          <ProgressRow bar={quality} step={state.step} />
        </div>
        <div className="space-y-2 sm:space-y-3">
          <ProgressRow bar={durability} step={state.step} />
          <ProgressRow bar={cp} step={state.step} />
        </div>
      </div>
    </div>
  );
}

interface CraftingStateBarProps {
  state: CraftingState;
  previousDeltas?: StepDeltas;
  compact?: boolean;
}

export function CraftingStateBar({
  state,
  previousDeltas,
  compact = false,
}: CraftingStateBarProps) {
  return compact ? (
    <CompactStateBar state={state} />
  ) : (
    <FullStateBar state={state} previousDeltas={previousDeltas} />
  );
}
