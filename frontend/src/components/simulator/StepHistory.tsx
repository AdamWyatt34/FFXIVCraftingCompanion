"use client";

import { useRef, useEffect, useState, useCallback } from "react";
import { Undo2 } from "lucide-react";
import { ConfirmDialog } from "@/components/ui";
import { getActionIconUrl, ACTION_CATEGORY_COLORS } from "@/lib/action-icons";
import { BUFF_DISPLAY } from "@/lib/action-icons";
import { diffBuffs } from "@/lib/buff-utils";
import type {
  StepResult,
  CraftingState,
  StepDeltas,
  BuffChange,
} from "@/types/simulator";

interface StepHistoryProps {
  steps: StepResult[];
  initialState: CraftingState;
  onUndoToStep: (stepNumber: number) => void;
  onUndoLast: () => void;
}

// ── Delta chip rendering ──

function DeltaChip({
  value,
  label,
  color,
}: {
  value: number;
  label: string;
  color: string;
}) {
  if (value === 0) return null;
  const sign = value > 0 ? "+" : "";
  return (
    <span className={`text-xs font-medium ${color}`}>
      {sign}
      {value.toLocaleString()} {label}
    </span>
  );
}

function DeltaRow({ deltas }: { deltas: StepDeltas }) {
  const chips = [
    { value: deltas.progress, label: "prog", color: "text-blue-400" },
    { value: deltas.quality, label: "qual", color: "text-emerald-400" },
    {
      value: deltas.durability,
      label: "dur",
      color: deltas.durability >= 0 ? "text-emerald-400" : "text-red-400",
    },
    { value: deltas.cp, label: "CP", color: "text-violet-400" },
  ].filter((c) => c.value !== 0);

  if (chips.length === 0) return null;

  return (
    <div className="flex flex-wrap gap-x-2 gap-y-0.5 mt-0.5">
      {chips.map((c) => (
        <DeltaChip
          key={c.label}
          value={c.value}
          label={c.label}
          color={c.color}
        />
      ))}
    </div>
  );
}

// ── Buff change rendering ──

function BuffChangeRow({ changes }: { changes: BuffChange[] }) {
  if (changes.length === 0) return null;

  return (
    <div className="flex flex-wrap gap-x-2 gap-y-0.5 mt-0.5">
      {changes.map((change) => {
        const display = BUFF_DISPLAY[change.name];
        const label = display?.abbreviation ?? change.name;

        if (change.type === "changed") {
          return (
            <span key={change.name} className="text-xs text-teal-400">
              IQ: {change.stacksBefore} → {change.stacksAfter}
            </span>
          );
        }
        if (change.type === "removed") {
          return (
            <span
              key={change.name}
              className="text-xs text-text-muted line-through"
            >
              {label}
            </span>
          );
        }
        // added
        return (
          <span key={change.name} className="text-xs text-amber-400">
            {label} ({change.turnsAfter})
          </span>
        );
      })}
    </div>
  );
}

// ── Single step entry ──

function StepHistoryItem({
  step,
  buffChanges,
  onUndo,
}: {
  step: StepResult;
  buffChanges: BuffChange[];
  onUndo: () => void;
}) {
  const categoryColor =
    ACTION_CATEGORY_COLORS[
      step.actionCategory as keyof typeof ACTION_CATEGORY_COLORS
    ] ?? "#6b7280";
  const isFailed = !step.succeeded;

  return (
    <div
      className="relative pl-3 pr-2 py-2 group animate-step-enter"
      style={{ borderLeftWidth: 3, borderLeftColor: categoryColor }}
    >
      {/* Row 1: Step number + icon + name + undo button */}
      <div className="flex items-center gap-1.5">
        <span className="text-xs text-text-muted font-mono w-5 shrink-0 text-right">
          {step.stepNumber}.
        </span>
        <img
          src={getActionIconUrl(step.actionName)}
          alt=""
          width={16}
          height={16}
          className={`shrink-0 ${isFailed ? "grayscale opacity-50" : ""}`}
        />
        <span
          className={`text-sm font-semibold truncate ${
            isFailed
              ? "line-through text-red-400"
              : "text-text-primary"
          }`}
        >
          {step.actionName}
        </span>
        {isFailed && (
          <span className="text-xs text-red-400 font-medium shrink-0">
            Failed
          </span>
        )}
        <button
          onClick={onUndo}
          className="ml-auto shrink-0 p-1 rounded text-text-muted hover:text-gold opacity-0 group-hover:opacity-100 transition-opacity"
          title={`Undo to step ${step.stepNumber}`}
        >
          <Undo2 size={12} />
        </button>
      </div>

      {/* Row 2: Deltas */}
      <div className="ml-[26px]">
        <DeltaRow deltas={step.deltas} />
        <BuffChangeRow changes={buffChanges} />
      </div>
    </div>
  );
}

// ── Main component ──

export function StepHistory({
  steps,
  initialState,
  onUndoToStep,
  onUndoLast,
}: StepHistoryProps) {
  const scrollRef = useRef<HTMLDivElement>(null);
  const bottomRef = useRef<HTMLDivElement>(null);
  const [undoConfirm, setUndoConfirm] = useState<{
    stepNumber: number;
    stepsToUndo: number;
  } | null>(null);

  // Auto-scroll to bottom on new steps
  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: "smooth" });
  }, [steps.length]);

  const handleUndoClick = useCallback(
    (stepNumber: number) => {
      const stepsToUndo = steps.length - stepNumber + 1;
      if (stepsToUndo > 3) {
        setUndoConfirm({ stepNumber, stepsToUndo });
      } else {
        // undoToStep expects the number of steps to keep (0-indexed slice)
        onUndoToStep(stepNumber - 1);
      }
    },
    [steps.length, onUndoToStep],
  );

  const handleUndoConfirm = useCallback(() => {
    if (undoConfirm) {
      onUndoToStep(undoConfirm.stepNumber - 1);
      setUndoConfirm(null);
    }
  }, [undoConfirm, onUndoToStep]);

  // Compute buff changes for each step
  const buffChangesPerStep = steps.map((step, i) => {
    const prevState = i === 0 ? initialState : steps[i - 1].stateAfter;
    return diffBuffs(
      prevState.activeBuffs,
      step.stateAfter.activeBuffs,
      prevState.innerQuietStacks,
      step.stateAfter.innerQuietStacks,
    );
  });

  return (
    <div className="flex flex-col h-full">
      <h3 className="text-sm font-semibold text-text-secondary mb-2 px-1">
        Step History
      </h3>

      {steps.length === 0 ? (
        <div className="flex-1 flex items-center justify-center px-4">
          <p className="text-sm text-text-muted text-center">
            No actions taken yet. Use the action grid below to start your
            rotation.
          </p>
        </div>
      ) : (
        <>
          <div
            ref={scrollRef}
            className="flex-1 overflow-y-auto space-y-0.5 min-h-0"
            role="list"
            aria-label="Step history"
          >
            {steps.map((step, i) => (
              <div key={i} role="listitem">
                <StepHistoryItem
                  step={step}
                  buffChanges={buffChangesPerStep[i]}
                  onUndo={() => handleUndoClick(step.stepNumber)}
                />
              </div>
            ))}
            <div ref={bottomRef} />
          </div>

          {/* Undo Last button at bottom */}
          <div className="pt-2 mt-2 border-t border-border">
            <button
              onClick={onUndoLast}
              className="flex items-center justify-center gap-1.5 w-full px-3 py-1.5 bg-bg-elevated text-text-secondary font-medium rounded-lg hover:text-text-primary border border-border transition-colors text-sm"
            >
              <Undo2 size={14} />
              Undo Last
            </button>
          </div>
        </>
      )}

      <ConfirmDialog
        open={undoConfirm !== null}
        title="Undo Multiple Steps?"
        message={
          undoConfirm
            ? `Undo ${undoConfirm.stepsToUndo} actions? This will return to step ${undoConfirm.stepNumber - 1}.`
            : ""
        }
        confirmLabel="Undo"
        confirmVariant="danger"
        onConfirm={handleUndoConfirm}
        onCancel={() => setUndoConfirm(null)}
      />
    </div>
  );
}
