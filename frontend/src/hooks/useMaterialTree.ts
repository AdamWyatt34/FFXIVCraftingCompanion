import { useState, useEffect, useRef, useCallback } from "react";
import { useDebounce } from "./useDebounce";
import { ApiClientError } from "@/lib/api-client";
import { getMaterialTree } from "@/lib/game-data";
import type { MaterialTreeResultDto } from "@/types/api";

interface UseMaterialTreeReturn {
  data: MaterialTreeResultDto | null;
  isLoading: boolean;
  error: string | null;
  retry: () => void;
}

const cache = new Map<string, MaterialTreeResultDto>();

export function useMaterialTree(
  recipeId: number,
  quantity: number,
): UseMaterialTreeReturn {
  const debouncedQuantity = useDebounce(quantity, 500);
  const abortRef = useRef<AbortController | null>(null);
  const [retryCount, setRetryCount] = useState(0);

  const [fetchResult, setFetchResult] = useState<{
    data: MaterialTreeResultDto | null;
    error: string | null;
    key: string;
  }>({ data: null, error: null, key: "" });

  const cacheKey = isNaN(recipeId)
    ? ""
    : `${recipeId}:${debouncedQuantity}`;
  const cachedData = cacheKey ? (cache.get(cacheKey) ?? null) : null;

  // Derive loading/data/error from cache + fetchResult during render (no setState in effect)
  const data =
    cachedData ?? (fetchResult.key === cacheKey ? fetchResult.data : null);
  const error = cachedData
    ? null
    : fetchResult.key === cacheKey
      ? fetchResult.error
      : null;
  const isLoading = !cachedData && fetchResult.key !== cacheKey && !!cacheKey;

  useEffect(() => {
    if (!cacheKey || cache.has(cacheKey)) return;

    abortRef.current?.abort();
    const controller = new AbortController();
    abortRef.current = controller;

    getMaterialTree(recipeId, debouncedQuantity, controller.signal)
      .then((result) => {
        if (controller.signal.aborted) return;
        cache.set(cacheKey, result);
        setFetchResult({ data: result, error: null, key: cacheKey });
      })
      .catch((err: unknown) => {
        if (controller.signal.aborted) return;
        const msg =
          err instanceof ApiClientError
            ? err.message
            : "Failed to load material tree. Please try again.";
        setFetchResult({ data: null, error: msg, key: cacheKey });
      });

    return () => controller.abort();
  }, [cacheKey, recipeId, debouncedQuantity, retryCount]);

  const retry = useCallback(() => {
    if (cacheKey) cache.delete(cacheKey);
    setFetchResult({ data: null, error: null, key: "" });
    setRetryCount((c) => c + 1);
  }, [cacheKey]);

  return { data, isLoading, error, retry };
}
