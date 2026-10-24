"use client";

import { useState, useCallback, useEffect, useRef, useMemo } from "react";
import type {
  SimulationResult,
  CraftingState,
  StepDeltas,
  StepResult,
} from "@/types/simulator";

export interface ReplaySpeed {
  label: string;
  multiplier: number;
  intervalMs: number;
}

export const REPLAY_SPEEDS: ReplaySpeed[] = [
  { label: "0.5x", multiplier: 0.5, intervalMs: 2000 },
  { label: "1x", multiplier: 1, intervalMs: 1000 },
  { label: "2x", multiplier: 2, intervalMs: 500 },
  { label: "4x", multiplier: 4, intervalMs: 250 },
];

export interface UseRotationReplayReturn {
  currentStepIndex: number;
  totalSteps: number;
  isPlaying: boolean;
  speed: ReplaySpeed;

  currentState: CraftingState;
  currentDeltas: StepDeltas | null;
  currentStep: StepResult | null;
  previousState: CraftingState;
  isAtStart: boolean;
  isAtEnd: boolean;

  goToStart: () => void;
  goToEnd: () => void;
  goNext: () => void;
  goPrev: () => void;
  goToStep: (index: number) => void;

  play: () => void;
  pause: () => void;
  togglePlay: () => void;
  setSpeed: (speed: ReplaySpeed) => void;
}

export function useRotationReplay(
  simulation: SimulationResult,
): UseRotationReplayReturn {
  const [currentStepIndex, setCurrentStepIndex] = useState(0);
  const [isPlaying, setIsPlaying] = useState(false);
  const [speed, setSpeedState] = useState<ReplaySpeed>(REPLAY_SPEEDS[1]); // 1x default
  const intervalRef = useRef<ReturnType<typeof setInterval> | null>(null);

  const totalSteps = simulation.steps.length;

  // Synthesize the initial state (step 0) from simulation data
  const initialState = useMemo((): CraftingState => {
    const ref = simulation.steps[0]?.stateAfter ?? simulation.finalState;
    return {
      progress: 0,
      maxProgress: ref.maxProgress,
      progressPercent: 0,
      quality: 0,
      maxQuality: ref.maxQuality,
      qualityPercent: 0,
      durability: ref.maxDurability,
      maxDurability: ref.maxDurability,
      cp: ref.maxCP,
      maxCP: ref.maxCP,
      step: 0,
      condition: "Normal",
      status: "InProgress",
      hqPercent: 0,
      activeBuffs: [],
      innerQuietStacks: 0,
      comboAction: 0,
      expedienceActive: false,
      isFirstStep: true,
      quickInnovationUsed: false,
      heartAndSoulUsed: false,
      trainedPerfectionUsed: false,
      carefulObservationCount: 0,
    };
  }, [simulation]);

  // Derive current state from step index
  const currentState = useMemo((): CraftingState => {
    if (currentStepIndex === 0) return initialState;
    return simulation.steps[currentStepIndex - 1].stateAfter;
  }, [currentStepIndex, initialState, simulation.steps]);

  const previousState = useMemo((): CraftingState => {
    if (currentStepIndex <= 1) return initialState;
    return simulation.steps[currentStepIndex - 2].stateAfter;
  }, [currentStepIndex, initialState, simulation.steps]);

  const currentDeltas = useMemo((): StepDeltas | null => {
    if (currentStepIndex === 0) return null;
    return simulation.steps[currentStepIndex - 1].deltas;
  }, [currentStepIndex, simulation.steps]);

  const currentStep = useMemo((): StepResult | null => {
    if (currentStepIndex === 0) return null;
    return simulation.steps[currentStepIndex - 1];
  }, [currentStepIndex, simulation.steps]);

  const isAtStart = currentStepIndex === 0;
  const isAtEnd = currentStepIndex >= totalSteps;

  // ── Interval management ──

  const stopInterval = useCallback(() => {
    if (intervalRef.current) {
      clearInterval(intervalRef.current);
      intervalRef.current = null;
    }
  }, []);

  // Cleanup on unmount
  useEffect(() => {
    return () => stopInterval();
  }, [stopInterval]);

  // Stop playing when we reach the end
  useEffect(() => {
    if (!(currentStepIndex >= totalSteps && isPlaying)) return;
    stopInterval();
    const id = requestAnimationFrame(() => setIsPlaying(false));
    return () => cancelAnimationFrame(id);
  }, [currentStepIndex, totalSteps, isPlaying, stopInterval]);

  // ── Navigation ──

  const goToStart = useCallback(() => {
    stopInterval();
    setIsPlaying(false);
    setCurrentStepIndex(0);
  }, [stopInterval]);

  const goToEnd = useCallback(() => {
    stopInterval();
    setIsPlaying(false);
    setCurrentStepIndex(totalSteps);
  }, [stopInterval, totalSteps]);

  const goNext = useCallback(() => {
    setCurrentStepIndex((prev) => Math.min(prev + 1, totalSteps));
  }, [totalSteps]);

  const goPrev = useCallback(() => {
    setCurrentStepIndex((prev) => Math.max(prev - 1, 0));
  }, []);

  const goToStep = useCallback(
    (index: number) => {
      stopInterval();
      setIsPlaying(false);
      setCurrentStepIndex(Math.max(0, Math.min(index, totalSteps)));
    },
    [stopInterval, totalSteps],
  );

  // ── Auto-play ──

  const startInterval = useCallback(
    (intervalMs: number) => {
      stopInterval();
      intervalRef.current = setInterval(() => {
        setCurrentStepIndex((prev) => {
          const next = prev + 1;
          if (next > totalSteps) {
            // Will be caught by the effect above
            return prev;
          }
          return next;
        });
      }, intervalMs);
    },
    [stopInterval, totalSteps],
  );

  const play = useCallback(() => {
    if (currentStepIndex >= totalSteps) return;
    setIsPlaying(true);
    startInterval(speed.intervalMs);
  }, [currentStepIndex, totalSteps, speed.intervalMs, startInterval]);

  const pause = useCallback(() => {
    stopInterval();
    setIsPlaying(false);
  }, [stopInterval]);

  const togglePlay = useCallback(() => {
    if (isPlaying) {
      pause();
    } else {
      // If at end, restart from beginning
      if (currentStepIndex >= totalSteps) {
        setCurrentStepIndex(0);
        setIsPlaying(true);
        // Small delay to let state update before starting interval
        setTimeout(() => startInterval(speed.intervalMs), 50);
      } else {
        play();
      }
    }
  }, [isPlaying, pause, play, currentStepIndex, totalSteps, startInterval, speed.intervalMs]);

  const setSpeed = useCallback(
    (newSpeed: ReplaySpeed) => {
      setSpeedState(newSpeed);
      if (isPlaying) {
        startInterval(newSpeed.intervalMs);
      }
    },
    [isPlaying, startInterval],
  );

  return {
    currentStepIndex,
    totalSteps,
    isPlaying,
    speed,
    currentState,
    currentDeltas,
    currentStep,
    previousState,
    isAtStart,
    isAtEnd,
    goToStart,
    goToEnd,
    goNext,
    goPrev,
    goToStep,
    play,
    pause,
    togglePlay,
    setSpeed,
  };
}
