"use client";

import { useState, useEffect, useRef, useCallback } from "react";
import { getRotationById, ApiClientError } from "@/lib/api-client";
import { simulateCraft } from "@/lib/crafting-engine";
import type { RotationDetail } from "@/types/solver";

// Heart and Soul, Quick Innovation and Careful Observation need the specialist flag, which a
// saved rotation does not keep; replayed without it they would score as failed steps.
const SPECIALIST_ONLY_ACTION_IDS = new Set([100317, 100443, 100395]);

/** A saved rotation's metrics as the engine replays it; they can differ from the stored ones. */
export interface LiveRotationMetrics {
  hqPercent: number;
  qualityPercent: number;
  craftCompleted: boolean;
  totalSteps: number;
}

export interface UseRotationDetailReturn {
  rotation: RotationDetail | null;
  isLoading: boolean;
  error: string | null;
  retry: () => void;
  /** Null while recomputing, when the replay is skipped and when it fails; the stored values stand then. */
  live: LiveRotationMetrics | null;
  /** "specialist" when the rotation holds an action the replay cannot run without the specialist flag. */
  liveSkipReason: "specialist" | null;
  isRecomputing: boolean;
}

export function useRotationDetail(id: number): UseRotationDetailReturn {
  const [rotation, setRotation] = useState<RotationDetail | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const abortRef = useRef<AbortController | null>(null);

  const fetchRotation = useCallback(async () => {
    abortRef.current?.abort();
    const controller = new AbortController();
    abortRef.current = controller;

    setIsLoading(true);
    setError(null);

    try {
      const data = await getRotationById(id, controller.signal);
      if (controller.signal.aborted) return;
      setRotation(data);
    } catch (err) {
      if (err instanceof ApiClientError && err.status === 404) {
        setError("Rotation not found.");
      } else {
        setError(
          err instanceof ApiClientError
            ? err.body.error
            : "Failed to load rotation",
        );
      }
    } finally {
      setIsLoading(false);
    }
  }, [id]);

  useEffect(() => {
    fetchRotation();
    return () => {
      abortRef.current?.abort();
    };
  }, [fetchRotation]);

  // Kept with the rotation it was computed for, so a reload never shows another one's numbers.
  const [recomputed, setRecomputed] = useState<{
    rotation: RotationDetail;
    live: LiveRotationMetrics | null;
  } | null>(null);
  const liveSkipReason =
    rotation?.actionIds.some((actionId) => SPECIALIST_ONLY_ACTION_IDS.has(actionId)) ? "specialist" : null;

  useEffect(() => {
    if (!rotation || liveSkipReason) return;
    let cancelled = false;
    simulateCraft({
      recipeId: rotation.recipeId,
      stats: {
        level: rotation.crafterLevel,
        craftsmanship: rotation.craftsmanship,
        control: rotation.control,
        cp: rotation.cp,
      },
      actionIds: rotation.actionIds,
    })
      .then((result) => {
        if (cancelled) return;
        setRecomputed({
          rotation,
          live: {
            hqPercent: result.finalHQPercent,
            qualityPercent: result.finalQualityPercent,
            craftCompleted: result.success,
            totalSteps: result.totalSteps,
          },
        });
      })
      .catch((err) => {
        console.warn("Saved rotation could not be recomputed:", err);
        if (!cancelled) setRecomputed({ rotation, live: null });
      });
    return () => {
      cancelled = true;
    };
  }, [rotation, liveSkipReason]);

  const current = recomputed?.rotation === rotation ? recomputed : null;
  const isRecomputing = rotation !== null && liveSkipReason === null && current === null;

  return {
    rotation,
    isLoading,
    error,
    retry: fetchRotation,
    live: current?.live ?? null,
    liveSkipReason,
    isRecomputing,
  };
}
