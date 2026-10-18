"use client";

import { useState } from "react";
import {
  Settings2,
  ChevronDown,
  ChevronUp,
  RotateCcw,
  AlertTriangle,
} from "lucide-react";
import type { SolverConfig } from "@/hooks/useSolverConfig";
import { ALGORITHM_LABELS } from "@/lib/constants";

interface SolverConfigPanelProps {
  config: SolverConfig;
  onAlgorithmChange: (alg: SolverConfig["algorithm"]) => void;
  onTimeBudgetChange: (ms: number) => void;
  onObjectiveChange: (obj: SolverConfig["objective"]) => void;
  onTargetQualityChange: (pct: number | null) => void;
  onAllowUnsafeChange: (allow: boolean) => void;
  onResetDefaults: () => void;
  summaryText: string;
  disabled?: boolean;
}

const ALGORITHMS: SolverConfig["algorithm"][] = ["auto", "greedy", "mcts"];

const BUDGET_LABELS: Record<number, string> = {
  1: "Quick",
  10: "Balanced",
  30: "Thorough",
};

export function SolverConfigPanel({
  config,
  onAlgorithmChange,
  onTimeBudgetChange,
  onObjectiveChange,
  onTargetQualityChange,
  onAllowUnsafeChange,
  onResetDefaults,
  summaryText,
  disabled,
}: SolverConfigPanelProps) {
  const [isExpanded, setIsExpanded] = useState(false);

  const budgetSeconds = Math.round(config.timeBudgetMs / 1000);

  return (
    <div
      className={`rounded-lg border border-border bg-bg-surface ${disabled ? "opacity-60 pointer-events-none" : ""}`}
      role="group"
      aria-label="Solver configuration"
    >
      {/* Collapsed header / toggle */}
      <button
        onClick={() => setIsExpanded(!isExpanded)}
        className="w-full flex items-center gap-2 px-4 py-2.5 text-sm text-left hover:bg-bg-elevated transition-colors rounded-lg"
        aria-expanded={isExpanded}
        disabled={disabled}
      >
        <Settings2 size={14} className="text-text-muted shrink-0" />
        <span className="text-text-secondary flex-1 truncate">
          {summaryText}
        </span>
        {isExpanded ? (
          <ChevronUp size={14} className="text-text-muted shrink-0" />
        ) : (
          <ChevronDown size={14} className="text-text-muted shrink-0" />
        )}
      </button>

      {/* Expanded panel */}
      {isExpanded && (
        <div className="px-4 pb-4 space-y-4 animate-expand-down">
          {/* Algorithm selector */}
          <div role="group" aria-labelledby="solver-algorithm-label">
            <span id="solver-algorithm-label" className="block text-sm font-medium text-text-secondary mb-2">
              Algorithm
            </span>
            <div className="flex flex-wrap gap-1.5">
              {ALGORITHMS.map((alg) => {
                const label = ALGORITHM_LABELS[alg];
                const isActive = config.algorithm === alg;
                return (
                  <span key={alg} title={label?.description}>
                    <button
                      onClick={() => onAlgorithmChange(alg)}
                      aria-pressed={isActive}
                      className={`relative px-3 py-1.5 rounded-lg text-sm font-medium border transition-colors ${
                        isActive
                          ? "border-gold bg-gold/10 text-gold"
                          : "border-border text-text-secondary hover:text-text-primary hover:border-border-focus"
                      }`}
                    >
                      {label?.label ?? alg}
                      {alg === "auto" && (
                        <span className="ml-1.5 text-[10px] font-semibold text-gold/80 bg-gold/10 px-1 py-0.5 rounded">
                          REC
                        </span>
                      )}
                    </button>
                  </span>
                );
              })}
            </div>
          </div>

          {/* Time budget slider — hidden for greedy */}
          {config.algorithm !== "greedy" && (
            <div className="animate-expand-down">
              <label
                htmlFor="time-budget"
                className="flex items-center justify-between text-sm font-medium text-text-secondary mb-2"
              >
                <span>Time Budget</span>
                <span className="text-text-primary font-bold tabular-nums">
                  {budgetSeconds}s
                </span>
              </label>
              <input
                id="time-budget"
                type="range"
                min={1}
                max={30}
                step={1}
                value={budgetSeconds}
                onChange={(e) =>
                  onTimeBudgetChange(parseInt(e.target.value, 10) * 1000)
                }
                className="w-full accent-[#e6b422] cursor-pointer"
                aria-valuemin={1}
                aria-valuemax={30}
                aria-valuenow={budgetSeconds}
                aria-valuetext={`${budgetSeconds} seconds`}
              />
              <div className="flex justify-between text-xs text-text-muted mt-1">
                {Object.entries(BUDGET_LABELS).map(([sec, label]) => (
                  <span key={sec}>{label}</span>
                ))}
              </div>
            </div>
          )}

          {/* Objective toggle */}
          <div role="group" aria-labelledby="solver-objective-label">
            <span id="solver-objective-label" className="block text-sm font-medium text-text-secondary mb-2">
              Objective
            </span>
            <div className="flex rounded-lg border border-border overflow-hidden">
              <button
                onClick={() => onObjectiveChange("quality")}
                className={`flex-1 px-3 py-1.5 text-sm font-medium transition-colors ${
                  config.objective === "quality"
                    ? "bg-gold/10 text-gold border-r border-gold/30"
                    : "text-text-secondary hover:text-text-primary border-r border-border"
                }`}
                aria-pressed={config.objective === "quality"}
              >
                Maximize Quality
              </button>
              <span title="Finds the shortest rotation that completes the craft. Useful for macro slot efficiency.">
                <button
                  onClick={() => onObjectiveChange("steps")}
                  className={`flex-1 px-3 py-1.5 text-sm font-medium transition-colors ${
                    config.objective === "steps"
                      ? "bg-gold/10 text-gold"
                      : "text-text-secondary hover:text-text-primary"
                  }`}
                  aria-pressed={config.objective === "steps"}
                >
                  Minimize Steps
                </button>
              </span>
            </div>
          </div>

          {/* Target quality input — only for quality objective */}
          {config.objective === "quality" && (
            <div className="animate-expand-down">
              <label
                htmlFor="target-quality"
                className="block text-sm font-medium text-text-secondary mb-2"
              >
                Target Quality %
              </label>
              <div className="flex items-center gap-2">
                <input
                  id="target-quality"
                  type="number"
                  min={1}
                  max={100}
                  value={config.targetQualityPercent ?? ""}
                  onChange={(e) => {
                    const val = e.target.value;
                    onTargetQualityChange(
                      val === "" ? null : parseInt(val, 10),
                    );
                  }}
                  placeholder="Leave empty for max"
                  className="flex-1 px-3 py-1.5 bg-bg-elevated border border-border rounded-lg text-sm text-text-primary placeholder:text-text-muted focus:border-gold focus:outline-none transition-colors"
                />
                <span title="Stop searching once this HQ% is achievable. Use 100 for guaranteed HQ.">
                  <span className="text-text-muted text-xs cursor-help">?</span>
                </span>
              </div>
            </div>
          )}

          {/* Allow risky actions */}
          <label className="flex items-start gap-2 cursor-pointer">
            <input
              type="checkbox"
              checked={config.allowUnsafeActions}
              onChange={(e) => onAllowUnsafeChange(e.target.checked)}
              className="mt-0.5 accent-[#e6b422] cursor-pointer"
            />
            <div>
              <div className="text-sm text-text-primary flex items-center gap-1.5">
                <AlertTriangle size={12} className="text-amber-400 shrink-0" />
                Allow risky actions
              </div>
              <div className="text-xs text-text-muted mt-0.5">
                Rapid Synthesis, Hasty Touch — may fail in macros
              </div>
            </div>
          </label>

          {/* Reset */}
          <button
            onClick={onResetDefaults}
            className="flex items-center gap-1.5 text-xs text-text-secondary hover:text-gold transition-colors"
          >
            <RotateCcw size={12} />
            Reset to Defaults
          </button>
        </div>
      )}
    </div>
  );
}
