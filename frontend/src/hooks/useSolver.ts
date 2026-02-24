"use client";

import { useState, useRef, useCallback, useEffect } from "react";
import type {
  SolverJobStatus,
  SolverResult,
  SolveRequest,
  QuickSolveParams,
} from "@/types/solver";
import { isSolverJob } from "@/types/solver";
import * as Sentry from "@sentry/nextjs";
import {
  submitSolveJob,
  getSolveJob,
  cancelSolveJob,
  quickSolve,
  ApiClientError,
  trackAnalyticsEvent,
} from "@/lib/api-client";
import { SOLVER_CONSTANTS } from "@/lib/constants";

export interface UseSolverReturn {
  // State
  solverStatus: SolverJobStatus | "Idle";
  solverResult: SolverResult | null;
  quickResult: SolverResult | null;
  elapsedMs: number;
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

export function useSolver(): UseSolverReturn {
  const [solverStatus, setSolverStatus] = useState<SolverJobStatus | "Idle">("Idle");
  const [solverResult, setSolverResult] = useState<SolverResult | null>(null);
  const [quickResult, setQuickResult] = useState<SolverResult | null>(null);
  const [elapsedMs, setElapsedMs] = useState(0);
  const [error, setError] = useState<string | null>(null);
  const [jobId, setJobId] = useState<string | null>(null);
  const [timeBudgetMs, setTimeBudgetMs] = useState<number>(SOLVER_CONSTANTS.DEFAULT_TIME_BUDGET_MS);

  const abortRef = useRef<AbortController | null>(null);
  const pollRef = useRef<ReturnType<typeof setInterval> | null>(null);
  const jobIdRef = useRef<string | null>(null);

  // Keep jobIdRef in sync for use in cleanup/polling closures
  useEffect(() => {
    jobIdRef.current = jobId;
  }, [jobId]);

  const stopPolling = useCallback(() => {
    if (pollRef.current) {
      clearInterval(pollRef.current);
      pollRef.current = null;
    }
  }, []);

  const cleanup = useCallback(() => {
    abortRef.current?.abort();
    stopPolling();
  }, [stopPolling]);

  // Cleanup on unmount
  useEffect(() => {
    return () => {
      abortRef.current?.abort();
      if (pollRef.current) clearInterval(pollRef.current);
      // Fire-and-forget cancel for any running job
      if (jobIdRef.current) {
        cancelSolveJob(jobIdRef.current).catch(() => {});
      }
    };
  }, []);

  const startPolling = useCallback(
    (id: string, signal: AbortSignal) => {
      stopPolling();

      pollRef.current = setInterval(async () => {
        if (signal.aborted) {
          stopPolling();
          return;
        }

        try {
          const job = await getSolveJob(id, signal);
          if (signal.aborted) return;

          setSolverStatus(job.status);
          setElapsedMs(job.elapsedMs);

          if (job.status === "Completed" && job.result) {
            setSolverResult(job.result);
            stopPolling();
          } else if (job.status === "Failed" || job.status === "TimedOut") {
            setError(job.errorMessage ?? `Solve ${job.status.toLowerCase()}`);
            stopPolling();
          }
        } catch (err) {
          if (signal.aborted) return;
          if (err instanceof DOMException && err.name === "AbortError") return;

          const msg =
            err instanceof ApiClientError
              ? err.body.error
              : "Failed to check solve status";
          setError(msg);
          stopPolling();
        }
      }, SOLVER_CONSTANTS.POLL_INTERVAL_MS);
    },
    [stopPolling],
  );

  const startSolve = useCallback(
    async (request: SolveRequest) => {
      cleanup();

      const controller = new AbortController();
      abortRef.current = controller;

      setSolverStatus("Queued");
      setSolverResult(null);
      setQuickResult(null);
      setElapsedMs(0);
      setError(null);
      setJobId(null);

      const budget = request.settings?.timeBudgetMs ?? SOLVER_CONSTANTS.DEFAULT_TIME_BUDGET_MS;
      setTimeBudgetMs(budget);

      // Fire quick solve and MCTS in parallel
      const greedyPromise = request.stats
        ? quickSolve(
            {
              recipeId: request.recipeId,
              craftsmanship: request.stats.craftsmanship,
              control: request.stats.control,
              cp: request.stats.cp,
              level: request.stats.level,
            },
            controller.signal,
          )
        : null;

      const mctsPromise = submitSolveJob(request, controller.signal);

      // Handle greedy (non-blocking)
      if (greedyPromise) {
        greedyPromise
          .then((result) => {
            if (!controller.signal.aborted) {
              setQuickResult(result);
            }
          })
          .catch((err) => {
            if (controller.signal.aborted) return;
            if (err instanceof DOMException && err.name === "AbortError") return;
            // Greedy failure is non-fatal — MCTS may still succeed
            console.warn("Quick solve failed:", err);
          });
      }

      // Handle MCTS job submission
      try {
        const response = await mctsPromise;
        if (controller.signal.aborted) return;

        if (isSolverJob(response) && response.result) {
          // 200: cached result returned immediately
          setJobId(response.jobId);
          setSolverStatus("Completed");
          setSolverResult(response.result);
          setElapsedMs(response.elapsedMs);
          trackAnalyticsEvent("solver_run", { recipeId: request.recipeId, algorithm: request.settings?.algorithm, cached: true });
        } else {
          // 202: job enqueued — start polling
          const id = response.jobId;
          setJobId(id);
          setSolverStatus("Queued");
          startPolling(id, controller.signal);
        }
      } catch (err) {
        if (controller.signal.aborted) return;
        if (err instanceof DOMException && err.name === "AbortError") return;

        const msg =
          err instanceof ApiClientError
            ? err.body.error
            : "Failed to start solve";
        setError(msg);
        Sentry.withScope((scope) => {
          scope.setContext("solver", { recipeId: request.recipeId, algorithm: request.settings?.algorithm });
          Sentry.captureException(err);
        });
        // If greedy already fired, status stays usable; otherwise idle
        setSolverStatus("Failed");
      }
    },
    [cleanup, startPolling],
  );

  const startQuickSolve = useCallback(
    async (params: QuickSolveParams) => {
      cleanup();

      const controller = new AbortController();
      abortRef.current = controller;

      setSolverStatus("Running");
      setSolverResult(null);
      setQuickResult(null);
      setElapsedMs(0);
      setError(null);
      setJobId(null);

      try {
        const result = await quickSolve(params, controller.signal);
        if (controller.signal.aborted) return;

        setQuickResult(result);
        setSolverStatus("Completed");
      } catch (err) {
        if (controller.signal.aborted) return;
        if (err instanceof DOMException && err.name === "AbortError") return;

        const msg =
          err instanceof ApiClientError
            ? err.body.error
            : "Quick solve failed";
        setError(msg);
        setSolverStatus("Failed");
      }
    },
    [cleanup],
  );

  const cancelSolveAction = useCallback(async () => {
    cleanup();

    if (jobIdRef.current) {
      try {
        await cancelSolveJob(jobIdRef.current);
      } catch {
        // Ignore cancel errors (job may already be done)
      }
    }

    setSolverStatus((prev) =>
      prev === "Completed" ? "Completed" : "Idle",
    );
  }, [cleanup]);

  const clearResult = useCallback(() => {
    cleanup();

    if (jobIdRef.current) {
      cancelSolveJob(jobIdRef.current).catch(() => {});
    }

    setSolverStatus("Idle");
    setSolverResult(null);
    setQuickResult(null);
    setElapsedMs(0);
    setError(null);
    setJobId(null);
  }, [cleanup]);

  // Computed values
  const isSolving = solverStatus === "Queued" || solverStatus === "Running";
  const isLoading = isSolving || solverStatus === "Idle" && abortRef.current !== null && !abortRef.current.signal.aborted;
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
