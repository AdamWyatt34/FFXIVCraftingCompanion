"use client";

import { useState, useRef, useCallback, useEffect } from "react";
import type {
  SolverJobStatus,
  SolverResult,
  SolveRequest,
  QuickSolveParams,
} from "@/types/solver";
import type { SolverProgress } from "@/types/solver-worker";
import * as Sentry from "@sentry/nextjs";
import { ApiClientError, } from "@/lib/api-client";
import { trackAnalyticsEvent } from "@/lib/analytics";
import { startSolve as runSolve, quickSolve } from "@/lib/solver-client";
import { SOLVER_CONSTANTS } from "@/lib/constants";

export interface UseSolverReturn {
  // State
  solverStatus: SolverJobStatus | "Idle";
  solverResult: SolverResult | null;
  quickResult: SolverResult | null;
  elapsedMs: number;
  /** Live search statistics; null on the API path, which reports only status and time. */
  progress: SolverProgress | null;
  error: string | null;
  jobId: string | null;

  // Actions
  startSolve: (request: SolveRequest) => Promise<void>;
  startQuickSolve: (params: QuickSolveParams) => Promise<void>;
  cancelSolve: () => Promise<void>;
  clearResult: () => void;

  // Computed
  isLoading: boolean;
  isSolving: boolean;
  hasResult: boolean;
  progressPercent: number;
}

function isAbort(err: unknown): boolean {
  return err instanceof DOMException && err.name === "AbortError";
}

export function useSolver(): UseSolverReturn {
  const [solverStatus, setSolverStatus] = useState<SolverJobStatus | "Idle">(
    "Idle",
  );
  const [solverResult, setSolverResult] = useState<SolverResult | null>(null);
  const [quickResult, setQuickResult] = useState<SolverResult | null>(null);
  const [elapsedMs, setElapsedMs] = useState(0);
  const [progress, setProgress] = useState<SolverProgress | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [jobId, setJobId] = useState<string | null>(null);
  const [timeBudgetMs, setTimeBudgetMs] = useState<number>(
    SOLVER_CONSTANTS.DEFAULT_TIME_BUDGET_MS,
  );

  // Aborting the controller cancels the worker search or the API job, whichever is running.
  const abortRef = useRef<AbortController | null>(null);

  const cleanup = useCallback(() => {
    abortRef.current?.abort();
    abortRef.current = null;
  }, []);

  // Cleanup on unmount
  useEffect(() => {
    return () => {
      abortRef.current?.abort();
    };
  }, []);

  const resetState = useCallback(() => {
    setSolverResult(null);
    setQuickResult(null);
    setElapsedMs(0);
    setProgress(null);
    setError(null);
    setJobId(null);
  }, []);

  const startSolve = useCallback(
    async (request: SolveRequest) => {
      cleanup();

      const controller = new AbortController();
      abortRef.current = controller;

      setSolverStatus("Queued");
      resetState();

      const budget =
        request.settings?.timeBudgetMs ??
        SOLVER_CONSTANTS.DEFAULT_TIME_BUDGET_MS;
      setTimeBudgetMs(budget);

      // The greedy preview runs alongside the main solve, as before
      if (request.stats) {
        quickSolve(
          {
            recipeId: request.recipeId,
            craftsmanship: request.stats.craftsmanship,
            control: request.stats.control,
            cp: request.stats.cp,
            level: request.stats.level,
          },
          controller.signal,
        )
          .then((result) => {
            if (!controller.signal.aborted) setQuickResult(result);
          })
          .catch((err) => {
            if (controller.signal.aborted || isAbort(err)) return;
            // Greedy failure is non-fatal — the main solve may still succeed
            console.warn("Quick solve failed:", err);
          });
      }

      try {
        const job = await runSolve(request, {
          signal: controller.signal,
          onProgress: (update) => {
            if (controller.signal.aborted) return;
            setSolverStatus(update.status);
            setElapsedMs(update.elapsedMs);
            if (update.iterations !== undefined) setProgress(update);
          },
        });
        if (controller.signal.aborted) return;

        setJobId(job.jobId);
        setElapsedMs(job.elapsedMs);
        setSolverStatus(job.status);
        if (job.status === "Completed" && job.result) {
          setSolverResult(job.result);
          trackAnalyticsEvent("solver_run", {
            recipeId: request.recipeId,
            algorithm: request.settings?.algorithm,
            cached: job.jobId === "cached",
          });
        } else {
          setError(job.errorMessage ?? `Solve ${job.status.toLowerCase()}`);
        }
      } catch (err) {
        if (controller.signal.aborted || isAbort(err)) return;

        const msg =
          err instanceof ApiClientError
            ? err.body.error
            : "Failed to start solve";
        setError(msg);
        Sentry.withScope((scope) => {
          scope.setContext("solver", {
            recipeId: request.recipeId,
            algorithm: request.settings?.algorithm,
          });
          Sentry.captureException(err);
        });
        // If greedy already fired, status stays usable; otherwise idle
        setSolverStatus("Failed");
      }
    },
    [cleanup, resetState],
  );

  const startQuickSolve = useCallback(
    async (params: QuickSolveParams) => {
      cleanup();

      const controller = new AbortController();
      abortRef.current = controller;

      setSolverStatus("Running");
      resetState();

      try {
        const result = await quickSolve(params, controller.signal);
        if (controller.signal.aborted) return;

        setQuickResult(result);
        setSolverStatus("Completed");
      } catch (err) {
        if (controller.signal.aborted || isAbort(err)) return;

        const msg =
          err instanceof ApiClientError ? err.body.error : "Quick solve failed";
        setError(msg);
        setSolverStatus("Failed");
      }
    },
    [cleanup, resetState],
  );

  const cancelSolveAction = useCallback(async () => {
    cleanup();
    setSolverStatus((prev) => (prev === "Completed" ? "Completed" : "Idle"));
  }, [cleanup]);

  const clearResult = useCallback(() => {
    cleanup();
    setSolverStatus("Idle");
    resetState();
  }, [cleanup, resetState]);

  // Computed values
  const isSolving = solverStatus === "Queued" || solverStatus === "Running";
  const hasResult = solverResult !== null || quickResult !== null;

  let progressPercent = 0;
  if (solverStatus === "Completed") {
    progressPercent = 100;
  } else if (isSolving && timeBudgetMs > 0) {
    progressPercent = Math.min(95, (elapsedMs / timeBudgetMs) * 100);
  }

  return {
    solverStatus,
    solverResult,
    quickResult,
    elapsedMs,
    progress,
    error,
    jobId,
    startSolve,
    startQuickSolve,
    cancelSolve: cancelSolveAction,
    clearResult,
    isLoading: isSolving,
    isSolving,
    hasResult,
    progressPercent,
  };
}
