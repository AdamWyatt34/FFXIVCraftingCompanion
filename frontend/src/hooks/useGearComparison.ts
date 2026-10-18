import { useState, useEffect, useRef, useCallback } from "react";
import { useDebounce } from "@/hooks/useDebounce";
import { getGearComparison, ApiClientError } from "@/lib/api-client";
import type { GearComparisonDto } from "@/types/gear-comparison";

const comparisonCache = new Map<string, GearComparisonDto>();

interface UseGearComparisonReturn {
  comparison: GearComparisonDto | null;
  isLoading: boolean;
  error: string | null;
  retry: () => void;
  invalidate: () => void;
}

export function useGearComparison(
  job: string | null,
  level: number,
  profile: string,
  enabled: boolean,
): UseGearComparisonReturn {
  const debouncedLevel = useDebounce(level, 500);
  const [retryCount, setRetryCount] = useState(0);
  const abortRef = useRef<AbortController | null>(null);

  const [result, setResult] = useState<{
    data: GearComparisonDto | null;
    error: string | null;
    key: string;
  }>({ data: null, error: null, key: "" });

  const cacheKey =
    enabled && job && debouncedLevel >= 1
      ? `${job}:${debouncedLevel}:${profile}`
      : "";

  const cached = cacheKey ? (comparisonCache.get(cacheKey) ?? null) : null;
  const comparison = cached ?? (result.key === cacheKey ? result.data : null);
  const error = cached ? null : (result.key === cacheKey ? result.error : null);
  const isLoading = !cached && result.key !== cacheKey && !!cacheKey;

  useEffect(() => {
    if (!cacheKey || comparisonCache.has(cacheKey)) return;
    if (!job || debouncedLevel < 1 || !enabled) return;

    abortRef.current?.abort();
    const controller = new AbortController();
    abortRef.current = controller;

    getGearComparison(job, debouncedLevel, profile, controller.signal)
      .then((data) => {
        if (controller.signal.aborted) return;
        comparisonCache.set(cacheKey, data);
        setResult({ data, error: null, key: cacheKey });
      })
      .catch((err: unknown) => {
        if (controller.signal.aborted) return;
        const msg =
          err instanceof ApiClientError
            ? err.message
            : "Failed to load gear comparison.";
        setResult({ data: null, error: msg, key: cacheKey });
      });

    return () => controller.abort();
  }, [cacheKey, job, debouncedLevel, profile, enabled, retryCount]);

  useEffect(() => {
    return () => {
      abortRef.current?.abort();
    };
  }, []);

  const retry = useCallback(() => {
    if (cacheKey) comparisonCache.delete(cacheKey);
    setResult({ data: null, error: null, key: "" });
    setRetryCount((c) => c + 1);
  }, [cacheKey]);

  const invalidate = useCallback(() => {
    if (cacheKey) comparisonCache.delete(cacheKey);
    setResult({ data: null, error: null, key: "" });
    setRetryCount((c) => c + 1);
  }, [cacheKey]);

  return { comparison, isLoading, error, retry, invalidate };
}
