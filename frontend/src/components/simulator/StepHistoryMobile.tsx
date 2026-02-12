"use client";

import { useRef, useEffect, useState, useCallback } from "react";
import { Undo2, ChevronDown, ChevronUp } from "lucide-react";
import { getActionIconUrl, ACTION_CATEGORY_COLORS } from "@/lib/action-icons";
import { diffBuffs } from "@/lib/buff-utils";
import type { StepResult, CraftingState, BuffChange } from "@/types/simulator";

interface StepHistoryMobileProps {
  steps: StepResult[];
  initialState: CraftingState;
  onUndoToStep: (stepNumber: number) => void;
  onUndoLast: () => void;
}

// ── Compact step chip ──

function StepChip({
  step,
  isSelected,
  onTap,
}: {
  step: StepResult;
  isSelected: boolean;
  onTap: () => void;
}) {
  const categoryColor =
    ACTION_CATEGORY_COLORS[
      step.actionCategory as keyof typeof ACTION_CATEGORY_COLORS
    ] ?? "#6b7280";

  return (
    <button
      onClick={onTap}
      className={`shrink-0 flex items-center gap-1 px-1.5 py-1 rounded-md border transition-colors ${
        isSelected
          ? "border-gold bg-gold/10"
          : "border-border bg-bg-elevated hover:border-text-muted"
      } ${!step.succeeded ? "opacity-50" : ""}`}
      style={{ borderLeftWidth: 3, borderLeftColor: categoryColor }}
    >
      <img
        src={getActionIconUrl(step.actionName)}
        alt={step.actionName}
        width={20}
        height={20}
        className={!step.succeeded ? "grayscale" : ""}
      />
      <span className="text-[10px] text-text-muted font-mono">
        {step.stepNumber}
      </span>
    </button>
  );
}

// ── Step detail popover ──

function StepPopover({
  step,
  buffChanges,
  onUndoToHere,
  onClose,
}: {
  step: StepResult;
  buffChanges: BuffChange[];
  onUndoToHere: () => void;
  onClose: () => void;
}) {
  return (
    <div className="bg-bg-surface border border-border rounded-lg p-3 shadow-xl animate-scale-in">
      <div className="flex items-center justify-between mb-1.5">
        <div className="flex items-center gap-1.5">
          <img
            src={getActionIconUrl(step.actionName)}
            alt=""
            width={16}
            height={16}
          />
          <span
            className={`text-sm font-semibold ${
              !step.succeeded ? "line-through text-red-400" : "text-text-primary"
            }`}
          >
            Step {step.stepNumber}: {step.actionName}
          </span>
        </div>
        <button
          onClick={onClose}
          className="text-text-muted hover:text-text-secondary text-xs px-1"
        >
          &times;
        </button>
      </div>

      {/* Deltas */}
      <div className="flex flex-wrap gap-x-2 gap-y-0.5 text-xs mb-2">
        {step.deltas.progress !== 0 && (
          <span className="text-blue-400">
            {step.deltas.progress > 0 ? "+" : ""}
            {step.deltas.progress.toLocaleString()} prog
          </span>
        )}
        {step.deltas.quality !== 0 && (
          <span className="text-emerald-400">
            {step.deltas.quality > 0 ? "+" : ""}
            {step.deltas.quality.toLocaleString()} qual
          </span>
        )}
        {step.deltas.durability !== 0 && (
          <span
            className={
              step.deltas.durability >= 0 ? "text-emerald-400" : "text-red-400"
            }
          >
            {step.deltas.durability > 0 ? "+" : ""}
            {step.deltas.durability} dur
          </span>
        )}
        {step.deltas.cp !== 0 && (
          <span className="text-violet-400">
            {step.deltas.cp > 0 ? "+" : ""}
            {step.deltas.cp} CP
          </span>
        )}
      </div>

      {/* Buff changes */}
      {buffChanges.length > 0 && (
        <div className="flex flex-wrap gap-x-2 gap-y-0.5 text-xs mb-2">
          {buffChanges.map((change) => {
            if (change.type === "changed") {
              return (
                <span key={change.name} className="text-teal-400">
                  IQ: {change.stacksBefore} → {change.stacksAfter}
                </span>
              );
            }
            if (change.type === "removed") {
              return (
                <span
                  key={change.name}
                  className="text-text-muted line-through"
                >
                  {change.name}
                </span>
              );
            }
            return (
              <span key={change.name} className="text-amber-400">
                {change.name} ({change.turnsAfter})
              </span>
            );
          })}
        </div>
      )}

      <button
        onClick={onUndoToHere}
        className="flex items-center gap-1 w-full px-2 py-1 text-xs font-medium bg-bg-elevated border border-border rounded hover:text-text-primary text-text-secondary transition-colors"
      >
        <Undo2 size={12} />
        Undo to here
      </button>
    </div>
  );
}

// ── Main component ──

export function StepHistoryMobile({
  steps,
  initialState,
  onUndoToStep,
  onUndoLast,
}: StepHistoryMobileProps) {
  const stripRef = useRef<HTMLDivElement>(null);
  const [selectedIndex, setSelectedIndex] = useState<number | null>(null);
  const [expanded, setExpanded] = useState(false);

  // Auto-scroll strip to the right when new steps added
  useEffect(() => {
    if (stripRef.current) {
      stripRef.current.scrollLeft = stripRef.current.scrollWidth;
    }
  }, [steps.length]);

  const handleChipTap = useCallback(
    (index: number) => {
      setSelectedIndex(selectedIndex === index ? null : index);
    },
    [selectedIndex],
  );

  // Compute buff changes for selected step
  const getBuffChanges = (index: number): BuffChange[] => {
    const step = steps[index];
    const prevState = index === 0 ? initialState : steps[index - 1].stateAfter;
    return diffBuffs(
      prevState.activeBuffs,
      step.stateAfter.activeBuffs,
      prevState.innerQuietStacks,
      step.stateAfter.innerQuietStacks,
    );
  };

  if (steps.length === 0) return null;

  return (
    <div className="space-y-2">
      {/* Horizontal strip + undo button */}
      <div className="flex items-center gap-2">
        <div
          ref={stripRef}
          className="flex-1 flex items-center gap-1 overflow-x-auto py-1 scrollbar-none"
          style={{ scrollbarWidth: "none" }}
          role="list"
          aria-label="Step history"
        >
          {steps.map((step, i) => (
            <StepChip
              key={i}
              step={step}
              isSelected={selectedIndex === i}
              onTap={() => handleChipTap(i)}
            />
          ))}
        </div>
        <button
          onClick={onUndoLast}
          className="shrink-0 flex items-center gap-1 px-2 py-1.5 bg-bg-elevated border border-border rounded-lg text-text-secondary hover:text-text-primary text-xs font-medium transition-colors"
          title="Undo Last"
        >
          <Undo2 size={12} />
        </button>
      </div>

      {/* Popover for selected step */}
      {selectedIndex !== null && steps[selectedIndex] && (
        <StepPopover
          step={steps[selectedIndex]}
          buffChanges={getBuffChanges(selectedIndex)}
          onUndoToHere={() => {
            onUndoToStep(steps[selectedIndex].stepNumber - 1);
            setSelectedIndex(null);
          }}
          onClose={() => setSelectedIndex(null)}
        />
      )}

      {/* Expand toggle for full history */}
      {steps.length > 3 && (
        <button
          onClick={() => setExpanded(!expanded)}
          className="flex items-center gap-1 text-xs text-text-muted hover:text-text-secondary transition-colors mx-auto"
        >
          {expanded ? <ChevronUp size={12} /> : <ChevronDown size={12} />}
          {expanded
            ? "Hide history"
            : `Show full history (${steps.length} steps)`}
        </button>
      )}

      {/* Expanded full detail list */}
      {expanded && (
        <div className="bg-bg-surface border border-border rounded-lg p-2 max-h-64 overflow-y-auto animate-expand-down space-y-1">
          {steps.map((step, i) => {
            const buffChanges = getBuffChanges(i);
            const categoryColor =
              ACTION_CATEGORY_COLORS[
                step.actionCategory as keyof typeof ACTION_CATEGORY_COLORS
              ] ?? "#6b7280";

            return (
              <div
                key={i}
                className="flex items-start gap-2 pl-2 py-1"
                style={{
                  borderLeftWidth: 2,
                  borderLeftColor: categoryColor,
                }}
              >
                <span className="text-[10px] text-text-muted font-mono w-4 shrink-0 text-right pt-0.5">
                  {step.stepNumber}
                </span>
                <div className="flex-1 min-w-0">
                  <div className="flex items-center gap-1">
                    <img
                      src={getActionIconUrl(step.actionName)}
                      alt=""
                      width={14}
                      height={14}
                    />
                    <span
                      className={`text-xs font-semibold truncate ${
                        !step.succeeded
                          ? "line-through text-red-400"
                          : "text-text-primary"
                      }`}
                    >
                      {step.actionName}
                    </span>
                    {!step.succeeded && (
                      <span className="text-[10px] text-red-400">Failed</span>
                    )}
                  </div>
                  <div className="flex flex-wrap gap-x-1.5 text-[10px]">
                    {step.deltas.progress !== 0 && (
                      <span className="text-blue-400">
                        {step.deltas.progress > 0 ? "+" : ""}
                        {step.deltas.progress.toLocaleString()} prog
                      </span>
                    )}
                    {step.deltas.quality !== 0 && (
                      <span className="text-emerald-400">
                        {step.deltas.quality > 0 ? "+" : ""}
                        {step.deltas.quality.toLocaleString()} qual
                      </span>
                    )}
                    {step.deltas.durability !== 0 && (
                      <span
                        className={
                          step.deltas.durability >= 0
                            ? "text-emerald-400"
                            : "text-red-400"
                        }
                      >
                        {step.deltas.durability > 0 ? "+" : ""}
                        {step.deltas.durability} dur
                      </span>
                    )}
                    {step.deltas.cp !== 0 && (
                      <span className="text-violet-400">
                        {step.deltas.cp > 0 ? "+" : ""}
                        {step.deltas.cp} CP
                      </span>
                    )}
                    {buffChanges.map((change) => {
                      if (change.type === "changed") {
                        return (
                          <span key={change.name} className="text-teal-400">
                            IQ: {change.stacksBefore}→{change.stacksAfter}
                          </span>
                        );
                      }
                      if (change.type === "added") {
                        return (
                          <span key={change.name} className="text-amber-400">
                            {change.name} ({change.turnsAfter})
                          </span>
                        );
                      }
                      return null;
                    })}
                  </div>
                </div>
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}
