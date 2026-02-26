import { useState, useEffect, useCallback, useRef } from "react";
import { useDebounce } from "./useDebounce";
import { searchEquipment, ApiClientError } from "@/lib/api-client";
import type { EquipmentDto } from "@/types/gear";

const MIN_QUERY_LENGTH = 2;

interface UseEquipmentSearchReturn {
  query: string;
  setQuery: (q: string) => void;
  jobFilter: string | null;
  setJobFilter: (job: string | null) => void;
  slotFilter: string | null;
  setSlotFilter: (slot: string | null) => void;
  results: EquipmentDto[];
  isLoading: boolean;
  error: string | null;
  retry: () => void;
}

const searchCache = new Map<string, EquipmentDto[]>();

export function useEquipmentSearch(initialQuery = ""): UseEquipmentSearchReturn {
  const [query, setQuery] = useState(initialQuery);
  const [jobFilter, setJobFilter] = useState<string | null>(null);
  const [slotFilter, setSlotFilter] = useState<string | null>(null);
  const debouncedQuery = useDebounce(query, 300);
  const abortControllerRef = useRef<AbortController | null>(null);
  const [retryCount, setRetryCount] = useState(0);

  const [fetchResult, setFetchResult] = useState<{
    results: EquipmentDto[];
    error: string | null;
    forQuery: string;
    forJob: string | null;
    forSlot: string | null;
  }>({ results: [], error: null, forQuery: "", forJob: null, forSlot: null });

  const hasFilters = jobFilter !== null || slotFilter !== null;
  const isActive = debouncedQuery.length >= MIN_QUERY_LENGTH || hasFilters;

  const resultMatchesCurrent =
    fetchResult.forQuery === debouncedQuery &&
    fetchResult.forJob === jobFilter &&
    fetchResult.forSlot === slotFilter;

  const results = resultMatchesCurrent ? fetchResult.results : [];
  const error = resultMatchesCurrent ? fetchResult.error : null;
  const isFetching = isActive && !resultMatchesCurrent;

  useEffect(() => {
    if (!isActive) return;

    const cacheKey = `${debouncedQuery}:${jobFilter ?? ""}:${slotFilter ?? ""}`;
    const cached = searchCache.get(cacheKey);
    if (cached) {
      // Defer cached result to avoid synchronous setState in effect
      const id = requestAnimationFrame(() => {
        setFetchResult({
          results: cached,
          error: null,
          forQuery: debouncedQuery,
          forJob: jobFilter,
          forSlot: slotFilter,
        });
      });
      return () => cancelAnimationFrame(id);
    }

    abortControllerRef.current?.abort();
    const controller = new AbortController();
    abortControllerRef.current = controller;

    searchEquipment(
      debouncedQuery,
      jobFilter ?? undefined,
      slotFilter ?? undefined,
      undefined,
      undefined,
      controller.signal,
    )
      .then((data) => {
        if (controller.signal.aborted) return;
        searchCache.set(cacheKey, data);
        setFetchResult({
          results: data,
          error: null,
          forQuery: debouncedQuery,
          forJob: jobFilter,
          forSlot: slotFilter,
        });
      })
      .catch((err: unknown) => {
        if (controller.signal.aborted) return;
        if (err instanceof DOMException && err.name === "AbortError") return;
        const msg =
          err instanceof ApiClientError
            ? err.message
            : "An unexpected error occurred. Please try again.";
        setFetchResult({
          results: [],
          error: msg,
          forQuery: debouncedQuery,
          forJob: jobFilter,
          forSlot: slotFilter,
        });
      });

    return () => controller.abort();
  }, [debouncedQuery, jobFilter, slotFilter, retryCount, isActive]);

  const showLoading =
    isFetching ||
    (query.length >= MIN_QUERY_LENGTH && query !== debouncedQuery) ||
    (hasFilters && !resultMatchesCurrent);

  const retry = useCallback(() => {
    searchCache.clear();
    setFetchResult({ results: [], error: null, forQuery: "", forJob: null, forSlot: null });
    setRetryCount((c) => c + 1);
  }, []);

  return {
    query,
    setQuery,
    jobFilter,
    setJobFilter,
    slotFilter,
    setSlotFilter,
    results,
    isLoading: showLoading,
    error,
    retry,
  };
}
