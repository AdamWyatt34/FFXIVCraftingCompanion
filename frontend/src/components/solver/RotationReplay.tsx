"use client";

import { useEffect, useRef, useCallback, useMemo } from "react";
import { createPortal } from "react-dom";
import {
  X,
  SkipBack,
  SkipForward,
  ChevronLeft,
  ChevronRight,
  Play,
  Pause,
  Wrench,
} from "lucide-react";
import {
  useRotationReplay,
  REPLAY_SPEEDS,
} from "@/hooks/useRotationReplay";
import { CraftingStateBar } from "@/components/simulator/CraftingStateBar";
import { ActiveBuffsDisplay } from "@/components/simulator/ActiveBuffsDisplay";
import { ConditionIndicator } from "@/components/simulator/ConditionIndicator";
import { HQProbabilityDisplay } from "@/components/simulator/HQProbabilityDisplay";
import {
  getActionIconUrl,
  ACTION_CATEGORY_COLORS,
} from "@/lib/action-icons";
import { diffBuffs } from "@/lib/buff-utils";
import type { SimulationResult, ActionCategory } from "@/types/simulator";

interface RotationReplayProps {
  simulation: SimulationResult;
  initialStepIndex?: number;
  onClose: () => void;
  onLoadIntoSimulator: () => void;
}

export function RotationReplay({
  simulation,
  initialStepIndex,
  onClose,
  onLoadIntoSimulator,
}: RotationReplayProps) {
  const replay = useRotationReplay(simulation);
  const timelineRef = useRef<HTMLDivElement>(null);
  const activeStepRef = useRef<HTMLButtonElement>(null);
  const announcementRef = useRef<HTMLDivElement>(null);

  // Jump to initial step on mount
  useEffect(() => {
    if (initialStepIndex !== undefined && initialStepIndex > 0) {
      replay.goToStep(initialStepIndex);
    }
    // Only run on mount
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // Auto-scroll timeline to keep active step visible
  useEffect(() => {
    if (activeStepRef.current) {
      activeStepRef.current.scrollIntoView({
        behavior: "smooth",
        inline: "center",
        block: "nearest",
      });
    }
  }, [replay.currentStepIndex]);

  // Screen reader announcement
  useEffect(() => {
    if (announcementRef.current && replay.currentStep) {
      const step = replay.currentStep;
      const parts = [
        `Step ${replay.currentStepIndex} of ${replay.totalSteps}: ${step.actionName}`,
        step.deltas.progress !== 0 &&
          `${step.deltas.progress > 0 ? "+" : ""}${step.deltas.progress} progress`,
        step.deltas.quality !== 0 &&
          `${step.deltas.quality > 0 ? "+" : ""}${step.deltas.quality} quality`,
        step.deltas.durability !== 0 &&
          `${step.deltas.durability} durability`,
        step.deltas.cp !== 0 && `${step.deltas.cp} CP`,
      ]
        .filter(Boolean)
        .join(", ");
      announcementRef.current.textContent = parts;
    }
  }, [replay.currentStepIndex, replay.currentStep, replay.totalSteps]);

  // Keyboard navigation
  const handleKeyDown = useCallback(
    (e: KeyboardEvent) => {
      // Don't capture if user is in an input
      if (
        e.target instanceof HTMLInputElement ||
        e.target instanceof HTMLTextAreaElement
      )
        return;

      switch (e.key) {
        case "ArrowLeft":
          e.preventDefault();
          replay.goPrev();
          break;
        case "ArrowRight":
          e.preventDefault();
          replay.goNext();
          break;
        case " ":
          e.preventDefault();
          replay.togglePlay();
          break;
        case "Home":
          e.preventDefault();
          replay.goToStart();
          break;
        case "End":
          e.preventDefault();
          replay.goToEnd();
          break;
        case "Escape":
          e.preventDefault();
          onClose();
          break;
      }
    },
    [replay, onClose],
  );

  useEffect(() => {
    document.addEventListener("keydown", handleKeyDown);
    return () => document.removeEventListener("keydown", handleKeyDown);
  }, [handleKeyDown]);

  // Compute buff changes for current step
  const buffChanges = useMemo(() => {
    if (!replay.currentStep) return [];
    const prevState = replay.previousState;
    const curState = replay.currentState;
    return diffBuffs(
      prevState.activeBuffs,
      curState.activeBuffs,
      prevState.innerQuietStacks,
      curState.innerQuietStacks,
    );
  }, [replay.currentStep, replay.previousState, replay.currentState]);

  const content = (
    <div className="fixed inset-0 z-50 flex flex-col bg-bg-primary/95 backdrop-blur-sm">
      {/* SR announcement */}
      <div
        ref={announcementRef}
        className="sr-only"
        aria-live="polite"
        aria-atomic="true"
      />

      {/* Header */}
      <div className="flex items-center justify-between px-4 py-3 border-b border-border bg-bg-surface">
        <div className="flex items-center gap-3">
          <h2 className="text-lg font-bold text-text-primary">
            Step {replay.currentStepIndex} / {replay.totalSteps}
          </h2>
          {replay.currentStep && (
            <span
              className="text-sm text-text-secondary"
              aria-hidden="true"
            >
              &mdash; {replay.currentStep.actionName}
            </span>
          )}
        </div>
        <div className="flex items-center gap-2">
          <button
            onClick={onLoadIntoSimulator}
            className="flex items-center gap-1.5 px-3 py-1.5 bg-bg-elevated text-text-secondary font-medium rounded-lg hover:text-text-primary border border-border transition-colors text-sm"
          >
            <Wrench size={14} />
            <span className="hidden sm:inline">Load into Simulator</span>
          </button>
          <button
            onClick={onClose}
            className="p-1.5 text-text-muted hover:text-text-primary transition-colors rounded-lg hover:bg-bg-elevated"
            aria-label="Close replay"
          >
            <X size={20} />
          </button>
        </div>
      </div>

      {/* Scrollable main content */}
      <div className="flex-1 overflow-y-auto p-4 space-y-4 max-w-5xl mx-auto w-full">
        {/* State bars */}
        <CraftingStateBar
          state={replay.currentState}
          previousDeltas={replay.currentDeltas ?? undefined}
        />

        {/* Condition + Buffs + HQ row */}
        <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:gap-4">
          <div className="flex items-center gap-2 overflow-x-auto sm:overflow-visible flex-nowrap flex-1 min-w-0">
            <ConditionIndicator
              condition={replay.currentState.condition}
            />
            <div className="flex-1 min-w-0">
              <ActiveBuffsDisplay
                buffs={replay.currentState.activeBuffs}
                innerQuietStacks={replay.currentState.innerQuietStacks}
              />
            </div>
          </div>
          <HQProbabilityDisplay
            hqPercent={replay.currentState.hqPercent}
            compact
          />
        </div>

        {/* Current action detail */}
        {replay.currentStep && (
          <div
            key={replay.currentStepIndex}
            className="rounded-lg border border-border bg-bg-surface p-4 animate-replay-transition"
          >
            <div className="flex items-center gap-3 mb-3">
              {/* eslint-disable-next-line @next/next/no-img-element */}
              <img
                src={getActionIconUrl(replay.currentStep.actionName)}
                alt=""
                width={32}
                height={32}
                className="rounded"
              />
              <div>
                <div className="font-bold text-text-primary">
                  {replay.currentStep.actionName}
                </div>
                <span
                  className="inline-block text-xs px-1.5 py-0.5 rounded font-medium"
                  style={{
                    backgroundColor: `${ACTION_CATEGORY_COLORS[replay.currentStep.actionCategory as ActionCategory] ?? ACTION_CATEGORY_COLORS.Other}20`,
                    color:
                      ACTION_CATEGORY_COLORS[
                        replay.currentStep.actionCategory as ActionCategory
                      ] ?? ACTION_CATEGORY_COLORS.Other,
                  }}
                >
                  {replay.currentStep.actionCategory}
                </span>
              </div>
              {!replay.currentStep.succeeded && (
                <span className="ml-auto text-xs font-medium text-red-400 bg-red-400/10 px-2 py-1 rounded">
                  Failed
                </span>
              )}
            </div>

            {/* Deltas */}
            <div className="grid grid-cols-2 sm:grid-cols-4 gap-2 text-sm">
              {replay.currentStep.deltas.progress !== 0 && (
                <DeltaChip
                  label="Progress"
                  value={replay.currentStep.deltas.progress}
                  color="#60a5fa"
                />
              )}
              {replay.currentStep.deltas.quality !== 0 && (
                <DeltaChip
                  label="Quality"
                  value={replay.currentStep.deltas.quality}
                  color="#4ade80"
                />
              )}
              {replay.currentStep.deltas.durability !== 0 && (
                <DeltaChip
                  label="Durability"
                  value={replay.currentStep.deltas.durability}
                  color="#fbbf24"
                />
              )}
              {replay.currentStep.deltas.cp !== 0 && (
                <DeltaChip
                  label="CP"
                  value={replay.currentStep.deltas.cp}
                  color="#a78bfa"
                />
              )}
            </div>

            {/* Buff changes */}
            {buffChanges.length > 0 && (
              <div className="mt-2 flex flex-wrap gap-1.5">
                {buffChanges.map((change) => (
                  <span
                    key={change.name}
                    className={`text-xs px-2 py-0.5 rounded font-medium ${
                      change.type === "added"
                        ? "bg-emerald-400/10 text-emerald-400"
                        : change.type === "removed"
                          ? "bg-red-400/10 text-red-400 line-through"
                          : "bg-blue-400/10 text-blue-400"
                    }`}
                  >
                    {change.type === "added" && "+"}
                    {change.type === "removed" && "-"}
                    {change.name}
                    {change.turnsAfter !== undefined &&
                      ` (${change.turnsAfter}t)`}
                    {change.stacksAfter !== undefined &&
                      change.stacksBefore !== undefined &&
                      ` ${change.stacksBefore}→${change.stacksAfter}`}
                  </span>
                ))}
              </div>
            )}
          </div>
        )}

        {/* Initial state message */}
        {!replay.currentStep && (
          <div className="rounded-lg border border-border bg-bg-surface p-4 text-center text-text-secondary">
            Initial state before any actions. Press Next or Play to begin.
          </div>
        )}

        {/* Timeline strip */}
        <div>
          <div className="text-xs font-medium text-text-muted mb-1.5">
            Timeline
          </div>
          <div
            ref={timelineRef}
            className="flex gap-1 overflow-x-auto pb-2 scrollbar-thin snap-x snap-mandatory"
            role="listbox"
            aria-label="Rotation timeline"
            aria-activedescendant={
              replay.currentStepIndex > 0
                ? `timeline-step-${replay.currentStepIndex}`
                : undefined
            }
          >
            {simulation.steps.map((step, i) => {
              const stepIndex = i + 1; // 1-based
              const isCurrent = stepIndex === replay.currentStepIndex;
              const isCompleted = stepIndex < replay.currentStepIndex;
              const isFuture = stepIndex > replay.currentStepIndex;
              const color =
                ACTION_CATEGORY_COLORS[
                  step.actionCategory as ActionCategory
                ] ?? ACTION_CATEGORY_COLORS.Other;

              return (
                <span
                  key={i}
                  title={`${step.stepNumber}. ${step.actionName}`}
                >
                  <button
                    id={`timeline-step-${stepIndex}`}
                    ref={isCurrent ? activeStepRef : undefined}
                    onClick={() => replay.goToStep(stepIndex)}
                    className={`flex items-center gap-1 px-1.5 py-1 rounded text-xs shrink-0 transition-all ${
                      isCurrent
                        ? "ring-2 ring-gold bg-gold/15 animate-step-highlight"
                        : isCompleted
                          ? "bg-bg-elevated hover:bg-bg-surface"
                          : ""
                    } ${isFuture ? "opacity-40" : ""}`}
                    role="option"
                    aria-selected={isCurrent}
                    aria-label={`Step ${stepIndex}: ${step.actionName}`}
                  >
                    {/* eslint-disable-next-line @next/next/no-img-element */}
                    <img
                      src={getActionIconUrl(step.actionName)}
                      alt=""
                      width={16}
                      height={16}
                      className="rounded-sm"
                    />
                    <span
                      className="hidden sm:inline font-medium"
                      style={{ color: isCurrent ? "#e6b422" : color }}
                    >
                      {step.stepNumber}
                    </span>
                  </button>
                </span>
              );
            })}
          </div>
        </div>
      </div>

      {/* Navigation controls — sticky bottom */}
      <div className="border-t border-border bg-bg-surface px-4 py-3">
        <div className="max-w-5xl mx-auto flex flex-col sm:flex-row items-center gap-3">
          {/* Transport controls */}
          <div className="flex items-center gap-1">
            <NavButton
              onClick={replay.goToStart}
              disabled={replay.isAtStart}
              label="Go to start"
            >
              <SkipBack size={18} />
            </NavButton>
            <NavButton
              onClick={replay.goPrev}
              disabled={replay.isAtStart}
              label="Previous step"
            >
              <ChevronLeft size={18} />
            </NavButton>
            <button
              onClick={replay.togglePlay}
              className={`p-2.5 rounded-lg font-medium transition-colors ${
                replay.isPlaying
                  ? "bg-gold text-bg-primary hover:bg-gold-light"
                  : "bg-bg-elevated text-text-primary hover:bg-gold/20 hover:text-gold border border-border"
              }`}
              aria-label={replay.isPlaying ? "Pause auto-play" : "Start auto-play"}
            >
              {replay.isPlaying ? <Pause size={18} /> : <Play size={18} />}
            </button>
            <NavButton
              onClick={replay.goNext}
              disabled={replay.isAtEnd}
              label="Next step"
            >
              <ChevronRight size={18} />
            </NavButton>
            <NavButton
              onClick={replay.goToEnd}
              disabled={replay.isAtEnd}
              label="Go to end"
            >
              <SkipForward size={18} />
            </NavButton>
          </div>

          {/* Step counter */}
          <div className="text-sm text-text-secondary tabular-nums">
            Step {replay.currentStepIndex} / {replay.totalSteps}
          </div>

          {/* Speed selector */}
          <div className="flex items-center gap-1 ml-auto">
            <span className="text-xs text-text-muted mr-1">Speed:</span>
            {REPLAY_SPEEDS.map((s) => (
              <button
                key={s.label}
                onClick={() => replay.setSpeed(s)}
                className={`px-2 py-1 rounded text-xs font-medium transition-colors min-h-[44px] min-w-[44px] ${
                  replay.speed.multiplier === s.multiplier
                    ? "bg-gold text-bg-primary"
                    : "bg-bg-elevated text-text-secondary hover:text-text-primary border border-border"
                }`}
                aria-pressed={replay.speed.multiplier === s.multiplier}
                aria-label={`Set speed to ${s.label}`}
              >
                {s.label}
              </button>
            ))}
          </div>
        </div>
      </div>
    </div>
  );

  // Render as portal
  if (typeof document === "undefined") return null;
  return createPortal(content, document.body);
}

// ── Helper components ──

function NavButton({
  onClick,
  disabled,
  label,
  children,
}: {
  onClick: () => void;
  disabled: boolean;
  label: string;
  children: React.ReactNode;
}) {
  return (
    <button
      onClick={onClick}
      disabled={disabled}
      className="p-2 rounded-lg bg-bg-elevated text-text-secondary hover:text-text-primary border border-border transition-colors disabled:opacity-30 disabled:cursor-not-allowed"
      aria-label={label}
    >
      {children}
    </button>
  );
}

function DeltaChip({
  label,
  value,
  color,
}: {
  label: string;
  value: number;
  color: string;
}) {
  const isPositive = value > 0;
  return (
    <div
      className="flex items-center justify-between rounded px-2 py-1"
      style={{ backgroundColor: `${color}10` }}
    >
      <span className="text-xs text-text-muted">{label}</span>
      <span
        className="font-medium text-xs"
        style={{ color: isPositive ? "#4ade80" : "#f87171" }}
      >
        {isPositive ? "+" : ""}
        {value.toLocaleString()}
      </span>
    </div>
  );
}
