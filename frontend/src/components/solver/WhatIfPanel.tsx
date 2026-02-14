"use client";

import { useState, useCallback, useEffect, useRef, useMemo } from "react";
import { useRouter } from "next/navigation";
import { SlidersHorizontal, RotateCcw, Plus, ChevronDown, BarChart3 } from "lucide-react";
import { WhatIfChart } from "./WhatIfChart";
import { LoadingSpinner } from "@/components/ui/LoadingSpinner";
import { WHAT_IF_PRESETS, STAT_SLIDER_RANGE } from "@/lib/constants";
import type { UseWhatIfReturn } from "@/hooks/useWhatIf";
import type { WhatIfStatSet, WhatIfResult } from "@/types/solver";

type VariedStat = "craftsmanship" | "control" | "cp";

interface WhatIfPanelProps {
  whatIf: UseWhatIfReturn;
  recipeName?: string;
  recipeLevel?: number;
  craftType?: string;
}

function getHqColor(pct: number): string {
  if (pct >= 90) return "text-emerald-400";
  if (pct >= 60) return "text-amber-400";
  return "text-red-400";
}

function getHqBgColor(pct: number): string {
  if (pct >= 90) return "bg-emerald-500/10 border-emerald-500/30";
  if (pct >= 60) return "bg-amber-500/10 border-amber-500/30";
  return "bg-red-500/10 border-red-500/30";
}

const STAT_LABELS: Record<VariedStat, string> = {
  craftsmanship: "Craftsmanship",
  control: "Control",
  cp: "CP",
};

function StatSliderRow({
  label,
  statKey,
  baseValue,
  value,
  onChange,
  onVariedStatChange,
  isVaried,
}: {
  label: string;
  statKey: VariedStat;
  baseValue: number;
  value: number;
  onChange: (stat: VariedStat, value: number) => void;
  onVariedStatChange: (stat: VariedStat) => void;
  isVaried: boolean;
}) {
  const min = Math.max(1, baseValue - STAT_SLIDER_RANGE);
  const max = baseValue + STAT_SLIDER_RANGE;

  return (
    <div className="space-y-1">
      <div className="flex items-center justify-between">
        <label className="text-xs font-medium text-text-secondary">{label}</label>
        <span className="text-xs text-text-muted">
          Base: {baseValue}
          {value !== baseValue && (
            <span className={value > baseValue ? "text-emerald-400" : "text-red-400"}>
              {" "}({value > baseValue ? "+" : ""}{value - baseValue})
            </span>
          )}
        </span>
      </div>
      <div className="flex items-center gap-2">
        <button
          onClick={() => onChange(statKey, Math.max(min, value - 10))}
          className="w-8 h-8 flex items-center justify-center rounded bg-bg-elevated border border-border text-text-secondary hover:text-text-primary hover:border-border-focus transition-colors text-sm font-bold shrink-0"
          aria-label={`Decrease ${label} by 10`}
        >
          -
        </button>
        <input
          type="range"
          min={min}
          max={max}
          step={10}
          value={value}
          onChange={(e) => {
            onChange(statKey, Number(e.target.value));
            onVariedStatChange(statKey);
          }}
          className="flex-1 h-2 bg-bg-elevated rounded-lg appearance-none cursor-pointer accent-gold"
          aria-valuemin={min}
          aria-valuemax={max}
          aria-valuenow={value}
          aria-valuetext={`${label}: ${value}`}
          aria-label={`Adjust ${label}`}
        />
        <button
          onClick={() => onChange(statKey, Math.min(max, value + 10))}
          className="w-8 h-8 flex items-center justify-center rounded bg-bg-elevated border border-border text-text-secondary hover:text-text-primary hover:border-border-focus transition-colors text-sm font-bold shrink-0"
          aria-label={`Increase ${label} by 10`}
        >
          +
        </button>
        <input
          type="number"
          min={min}
          max={max}
          value={value}
          onChange={(e) => {
            const v = Number(e.target.value);
            if (!isNaN(v)) onChange(statKey, Math.max(min, Math.min(max, v)));
          }}
          className="w-20 h-8 bg-bg-elevated border border-border rounded text-text-primary text-sm text-center focus:border-border-focus outline-none shrink-0"
          aria-label={`${label} value`}
        />
      </div>
    </div>
  );
}

function ResultRow({
  result,
  isBase,
  onNavigate,
  onRetry,
}: {
  result: WhatIfResult;
  isBase: boolean;
  onNavigate: (stats: WhatIfStatSet) => void;
  onRetry: (result: WhatIfResult) => void;
}) {
  return (
    <tr
      className={`border-b border-border/50 hover:bg-bg-elevated/50 transition-colors cursor-pointer ${
        isBase ? "border-l-2 border-l-gold" : ""
      } ${!result.isLoading && !result.error ? "animate-row-highlight" : ""}`}
      onClick={() => !result.isLoading && !result.error && onNavigate(result.stats)}
      role="row"
    >
      <td className="py-2 px-3 text-sm text-text-primary font-medium">{result.label}</td>
      <td className="py-2 px-3 text-sm text-text-secondary text-right tabular-nums">{result.stats.craftsmanship}</td>
      <td className="py-2 px-3 text-sm text-text-secondary text-right tabular-nums">{result.stats.control}</td>
      <td className="py-2 px-3 text-sm text-text-secondary text-right tabular-nums">{result.stats.cp}</td>
      <td className="py-2 px-3 text-sm text-right tabular-nums">
        {result.isLoading ? (
          <LoadingSpinner size={14} />
        ) : result.error ? (
          <button
            onClick={(e) => {
              e.stopPropagation();
              onRetry(result);
            }}
            className="text-xs text-red-400 hover:text-red-300 transition-colors"
          >
            Retry
          </button>
        ) : (
          <span className={getHqColor(result.hqPercent)}>{result.hqPercent}%</span>
        )}
      </td>
      <td className="py-2 px-3 text-sm text-text-muted text-right tabular-nums">
        {result.isLoading ? "..." : result.error ? "-" : result.steps}
      </td>
      <td className="py-2 px-3 text-sm text-right">
        {result.isLoading ? (
          <span className="text-text-muted">...</span>
        ) : result.error ? (
          <span className="text-xs text-red-400" role="alert">{result.error}</span>
        ) : result.craftCompleted ? (
          <span className="text-emerald-400 text-xs">OK</span>
        ) : (
          <span className="text-red-400 text-xs">Fail</span>
        )}
      </td>
    </tr>
  );
}

function ResultCard({
  result,
  isBase,
  onNavigate,
  onRetry,
}: {
  result: WhatIfResult;
  isBase: boolean;
  onNavigate: (stats: WhatIfStatSet) => void;
  onRetry: (result: WhatIfResult) => void;
}) {
  return (
    <div
      className={`p-3 bg-bg-surface border rounded-lg space-y-2 ${
        isBase ? "border-gold/50" : "border-border"
      } ${!result.isLoading && !result.error ? "animate-row-highlight" : ""}`}
      onClick={() => !result.isLoading && !result.error && onNavigate(result.stats)}
      role="button"
      tabIndex={0}
      onKeyDown={(e) => {
        if (e.key === "Enter" && !result.isLoading && !result.error) onNavigate(result.stats);
      }}
    >
      <div className="flex items-center justify-between">
        <span className="text-sm font-medium text-text-primary">{result.label}</span>
        {result.isLoading ? (
          <LoadingSpinner size={14} />
        ) : result.error ? (
          <button
            onClick={(e) => { e.stopPropagation(); onRetry(result); }}
            className="text-xs text-red-400 hover:text-red-300"
          >
            Retry
          </button>
        ) : (
          <span className={`text-sm font-bold ${getHqColor(result.hqPercent)}`}>
            {result.hqPercent}% HQ
          </span>
        )}
      </div>
      <div className="flex items-center gap-3 text-xs text-text-muted">
        <span>{result.stats.craftsmanship}/{result.stats.control}/{result.stats.cp}</span>
        {!result.isLoading && !result.error && (
          <>
            <span>{result.steps} steps</span>
            <span className={result.craftCompleted ? "text-emerald-400" : "text-red-400"}>
              {result.craftCompleted ? "OK" : "Fail"}
            </span>
          </>
        )}
      </div>
      {result.error && (
        <p className="text-xs text-red-400" role="alert">{result.error}</p>
      )}
    </div>
  );
}

export function WhatIfPanel({
  whatIf,
  recipeName,
  recipeLevel,
  craftType,
}: WhatIfPanelProps) {
  const router = useRouter();
  const firstInputRef = useRef<HTMLInputElement>(null);
  const [showChart, setShowChart] = useState(false);
  const [adjustedStats, setAdjustedStats] = useState<WhatIfStatSet | null>(null);

  // Initialize adjusted stats from base stats
  useEffect(() => {
    if (whatIf.baseStats && !adjustedStats) {
      setAdjustedStats({ ...whatIf.baseStats });
    }
  }, [whatIf.baseStats, adjustedStats]);

  // Focus first input when panel opens
  useEffect(() => {
    const timer = setTimeout(() => firstInputRef.current?.focus(), 100);
    return () => clearTimeout(timer);
  }, []);

  const handleStatChange = useCallback(
    (stat: VariedStat, value: number) => {
      setAdjustedStats((prev) => {
        if (!prev) return prev;
        return { ...prev, [stat]: value };
      });
    },
    [],
  );

  const handleVariedStatChange = useCallback(
    (stat: VariedStat) => {
      if (adjustedStats) {
        whatIf.setBaseStats(adjustedStats);
      }
      whatIf.setVariedStat(stat);
    },
    [whatIf, adjustedStats],
  );

  const handleNavigateToSolve = useCallback(
    (stats: WhatIfStatSet) => {
      if (!whatIf.recipeId) return;
      const params = new URLSearchParams({
        recipeId: String(whatIf.recipeId),
        craftsmanship: String(stats.craftsmanship),
        control: String(stats.control),
        cp: String(stats.cp),
        level: String(stats.level),
        solve: "true",
      });
      router.push(`/simulator?${params.toString()}`);
    },
    [whatIf.recipeId, router],
  );

  const handleRetry = useCallback(
    (result: WhatIfResult) => {
      whatIf.addCustomEntry(result.stats, result.label);
    },
    [whatIf],
  );

  const handleAddCurrent = useCallback(() => {
    if (!adjustedStats) return;
    whatIf.addCustomEntry(adjustedStats, "Custom");
  }, [adjustedStats, whatIf]);

  const results = useMemo(() => whatIf.results, [whatIf.results]);

  if (!whatIf.baseStats) {
    return (
      <div className="p-4 bg-bg-surface border border-border rounded-lg text-center text-text-muted text-sm">
        Enter crafter stats above to use What If analysis
      </div>
    );
  }

  const base = whatIf.baseStats;

  return (
    <div
      className="bg-bg-surface border border-border rounded-lg p-4 space-y-4 animate-slide-up"
      aria-label="What If stat explorer"
    >
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-2">
          <SlidersHorizontal size={18} className="text-gold" />
          <h3 className="text-base font-semibold text-text-primary">What If Explorer</h3>
        </div>
        {recipeName && (
          <div className="text-xs text-text-muted">
            {recipeName} {recipeLevel ? `Lv.${recipeLevel}` : ""} {craftType ?? ""}
          </div>
        )}
      </div>

      {/* Main layout: 2-col on desktop, stacked on mobile */}
      <div className="lg:grid lg:grid-cols-2 lg:gap-6 space-y-4 lg:space-y-0">
        {/* Left: Stat controls */}
        <div className="space-y-4">
          {/* Stat sliders */}
          <div className="space-y-3">
            <StatSliderRow
              label="Craftsmanship"
              statKey="craftsmanship"
              baseValue={base.craftsmanship}
              value={adjustedStats?.craftsmanship ?? base.craftsmanship}
              onChange={handleStatChange}
              onVariedStatChange={handleVariedStatChange}
              isVaried={whatIf.variedStat === "craftsmanship"}
            />
            <StatSliderRow
              label="Control"
              statKey="control"
              baseValue={base.control}
              value={adjustedStats?.control ?? base.control}
              onChange={handleStatChange}
              onVariedStatChange={handleVariedStatChange}
              isVaried={whatIf.variedStat === "control"}
            />
            <StatSliderRow
              label="CP"
              statKey="cp"
              baseValue={base.cp}
              value={adjustedStats?.cp ?? base.cp}
              onChange={handleStatChange}
              onVariedStatChange={handleVariedStatChange}
              isVaried={whatIf.variedStat === "cp"}
            />
          </div>

          {/* Preset buttons */}
          <div className="space-y-2">
            <p className="text-xs font-medium text-text-muted uppercase tracking-wider">Presets</p>
            <div className="flex flex-wrap gap-1.5">
              {WHAT_IF_PRESETS.map((preset) => (
                <button
                  key={preset.key}
                  onClick={() => whatIf.applyPreset(preset.key)}
                  disabled={whatIf.isBisLoading && preset.key === "bis"}
                  className={`px-3 py-1.5 rounded-full text-xs font-medium border transition-colors ${
                    whatIf.activePreset === preset.key
                      ? "bg-gold/20 text-gold border-gold/50"
                      : "bg-bg-elevated text-text-secondary border-border hover:text-text-primary hover:border-border-focus"
                  } disabled:opacity-50`}
                  title={preset.description}
                >
                  {whatIf.isBisLoading && preset.key === "bis" ? (
                    <span className="flex items-center gap-1">
                      <LoadingSpinner size={10} />
                      BiS
                    </span>
                  ) : (
                    preset.label
                  )}
                </button>
              ))}
            </div>
          </div>

          {/* Add current stats button */}
          <button
            onClick={handleAddCurrent}
            className="flex items-center gap-1.5 px-3 py-1.5 text-xs text-text-secondary hover:text-gold border border-border rounded-lg hover:border-gold/30 transition-colors"
          >
            <Plus size={14} />
            Add Current Stats to Table
          </button>
        </div>

        {/* Right: Results + Chart */}
        <div className="space-y-4">
          {/* Rate limit banner */}
          {results.some((r) => r.error?.includes("Too many requests")) && (
            <div className="p-2 bg-amber-500/10 border border-amber-500/30 rounded-lg text-xs text-amber-400" role="alert">
              Too many requests. Try again in a few seconds.
            </div>
          )}

          {/* Results table (desktop) */}
          {results.length > 0 && (
            <div className="hidden sm:block" aria-busy={whatIf.isAnyLoading} aria-live="polite">
              <div className="overflow-x-auto">
                <table className="w-full text-left">
                  <thead>
                    <tr className="border-b border-border">
                      <th scope="col" className="py-2 px-3 text-xs font-medium text-text-muted">Label</th>
                      <th scope="col" className="py-2 px-3 text-xs font-medium text-text-muted text-right">Craft</th>
                      <th scope="col" className="py-2 px-3 text-xs font-medium text-text-muted text-right">Control</th>
                      <th scope="col" className="py-2 px-3 text-xs font-medium text-text-muted text-right">CP</th>
                      <th scope="col" className="py-2 px-3 text-xs font-medium text-text-muted text-right">HQ%</th>
                      <th scope="col" className="py-2 px-3 text-xs font-medium text-text-muted text-right">Steps</th>
                      <th scope="col" className="py-2 px-3 text-xs font-medium text-text-muted text-right">Status</th>
                    </tr>
                  </thead>
                  <tbody>
                    {results.map((r) => (
                      <ResultRow
                        key={r.id}
                        result={r}
                        isBase={r.label === "Current" || r.label === "Current Gear"}
                        onNavigate={handleNavigateToSolve}
                        onRetry={handleRetry}
                      />
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          )}

          {/* Results cards (mobile) */}
          {results.length > 0 && (
            <div className="sm:hidden space-y-2" aria-busy={whatIf.isAnyLoading} aria-live="polite">
              {results.map((r) => (
                <ResultCard
                  key={r.id}
                  result={r}
                  isBase={r.label === "Current" || r.label === "Current Gear"}
                  onNavigate={handleNavigateToSolve}
                  onRetry={handleRetry}
                />
              ))}
            </div>
          )}

          {/* Empty state */}
          {results.length === 0 && (
            <div className="p-6 text-center text-text-muted text-sm">
              <SlidersHorizontal size={32} className="mx-auto mb-2 opacity-30" />
              <p>Drag a stat slider or click a preset to see how different stats affect your results</p>
            </div>
          )}

          {/* Chart toggle (mobile) */}
          {whatIf.chartData.length >= 2 && (
            <button
              onClick={() => setShowChart(!showChart)}
              className="lg:hidden flex items-center gap-1.5 text-xs text-text-secondary hover:text-gold transition-colors"
            >
              <BarChart3 size={14} />
              {showChart ? "Hide Chart" : "Show Chart"}
              <ChevronDown size={12} className={`transition-transform ${showChart ? "rotate-180" : ""}`} />
            </button>
          )}

          {/* Chart (always visible on desktop if data available, toggled on mobile) */}
          {whatIf.chartData.length >= 2 && (
            <div className={`${showChart ? "block" : "hidden"} lg:block`}>
              <WhatIfChart
                data={whatIf.chartData}
                statLabel={whatIf.variedStat ? STAT_LABELS[whatIf.variedStat] : "Stat"}
                currentStatValue={whatIf.variedStat && base ? base[whatIf.variedStat] : undefined}
                currentHqPercent={
                  results.find((r) => r.label === "Current")?.hqPercent
                }
              />
            </div>
          )}
        </div>
      </div>

      {/* Clear all button */}
      {results.length > 0 && (
        <div className="flex justify-end">
          <button
            onClick={whatIf.clearAll}
            className="flex items-center gap-1.5 text-xs text-text-muted hover:text-text-secondary transition-colors"
          >
            <RotateCcw size={12} />
            Clear All
          </button>
        </div>
      )}
    </div>
  );
}
