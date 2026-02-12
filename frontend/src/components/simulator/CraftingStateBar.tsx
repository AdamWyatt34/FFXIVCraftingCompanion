"use client";

import type { CraftingState, StepDeltas } from "@/types/simulator";

interface ProgressRowProps {
  label: string;
  shortLabel: string;
  current: number;
  max: number;
  percent: number;
  colorFrom: string;
  colorTo: string;
  warningFrom?: string;
  warningTo?: string;
  warningThreshold?: number;
  delta?: number;
  step: number;
}

function ProgressRow({
  label,
  shortLabel,
  current,
  max,
  percent,
  colorFrom,
  colorTo,
  warningFrom,
  warningTo,
  warningThreshold,
  delta,
  step,
}: ProgressRowProps) {
  const isWarning =
    warningThreshold !== undefined &&
    warningThreshold > 0 &&
    percent <= warningThreshold;

  const fromColor = isWarning && warningFrom ? warningFrom : colorFrom;
  const toColor = isWarning && warningTo ? warningTo : colorTo;

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
            background: `linear-gradient(to right, ${fromColor}, ${toColor})`,
            transition: "width 0.3s ease-out, background 0.3s ease",
          }}
        />
      </div>
    </div>
  );
}

interface CraftingStateBarProps {
  state: CraftingState;
  previousDeltas?: StepDeltas;
}

export function CraftingStateBar({
  state,
  previousDeltas,
}: CraftingStateBarProps) {
  const durabilityPercent =
    state.maxDurability > 0
      ? (state.durability / state.maxDurability) * 100
      : 0;
  const cpPercent =
    state.maxCP > 0 ? (state.cp / state.maxCP) * 100 : 0;

  return (
    <div className="bg-bg-surface rounded-lg border border-border p-3 sm:p-4">
      <div className="grid grid-cols-2 gap-2 sm:grid-cols-1 sm:gap-3">
        <div className="space-y-2 sm:space-y-3">
          <ProgressRow
            label="Progress"
            shortLabel="Prog"
            current={state.progress}
            max={state.maxProgress}
            percent={state.progressPercent}
            colorFrom="#60a5fa"
            colorTo="#3b82f6"
            delta={previousDeltas?.progress}
            step={state.step}
          />
          <ProgressRow
            label="Quality"
            shortLabel="Qual"
            current={state.quality}
            max={state.maxQuality}
            percent={state.qualityPercent}
            colorFrom="#4ade80"
            colorTo="#22c55e"
            delta={previousDeltas?.quality}
            step={state.step}
          />
        </div>
        <div className="space-y-2 sm:space-y-3">
          <ProgressRow
            label="Durability"
            shortLabel="Dur"
            current={state.durability}
            max={state.maxDurability}
            percent={durabilityPercent}
            colorFrom="#fbbf24"
            colorTo="#f59e0b"
            warningFrom="#ef4444"
            warningTo="#dc2626"
            warningThreshold={20}
            delta={previousDeltas?.durability}
            step={state.step}
          />
          <ProgressRow
            label="CP"
            shortLabel="CP"
            current={state.cp}
            max={state.maxCP}
            percent={cpPercent}
            colorFrom="#a78bfa"
            colorTo="#8b5cf6"
            warningFrom="#ef4444"
            warningTo="#dc2626"
            warningThreshold={15}
            delta={previousDeltas?.cp}
            step={state.step}
          />
        </div>
      </div>
    </div>
  );
}
