import { useState, useEffect, useCallback, useRef } from "react";
import { useDebounce } from "./useDebounce";
import { searchItems, ApiClientError } from "@/lib/api-client";
import { DEFAULT_PAGE_SIZE, MIN_SEARCH_LENGTH } from "@/lib/constants";
import type { ItemDto } from "@/types/api";

interface UseItemSearchReturn {
  query: string;
  setQuery: (q: string) => void;
  craftType: string | null;
  setCraftType: (ct: string | null) => void;
  items: ItemDto[];
  totalCount: number;
  isLoading: boolean;
  isLoadingMore: boolean;
  error: string | null;
  hasMore: boolean;
  loadMore: () => void;
  retry: () => void;
}

export function useItemSearch(initialQuery = ""): UseItemSearchReturn {
  const [query, setQueryRaw] = useState(initialQuery);
  const [craftType, setCraftTypeRaw] = useState<string | null>(null);
  const debouncedQuery = useDebounce(query, 300);
  const abortControllerRef = useRef<AbortController | null>(null);
  const [page, setPage] = useState(1);
  const [retryCount, setRetryCount] = useState(0);

  // Single state object for fetch results — only updated in async callbacks
  const [fetchResult, setFetchResult] = useState<{
    items: ItemDto[];
    totalCount: number;
    error: string | null;
    forQuery: string;
    forCraftType: string | null;
    forPage: number;
  }>({ items: [], totalCount: 0, error: null, forQuery: "", forCraftType: null, forPage: 0 });

  // When setting craft type, clear the text query
  const setCraftType = useCallback((ct: string | null) => {
    setCraftTypeRaw(ct);
    setQueryRaw("");
    setPage(1);
  }, []);

  // When setting query, clear the craft type filter
  const setQuery = useCallback((q: string) => {
    setQueryRaw(q);
    setCraftTypeRaw(null);
    setPage(1);
  }, []);

  // Reset page when debounced query or craft type changes (render-time)
  const [prevKey, setPrevKey] = useState(`${debouncedQuery}|${craftType}`);
  const currentKey = `${debouncedQuery}|${craftType}`;
  if (prevKey !== currentKey) {
    setPrevKey(currentKey);
    setPage(1);
  }

  // Determine if we have an active search/filter
  const hasTextQuery = debouncedQuery.length >= MIN_SEARCH_LENGTH;
  const hasFilter = craftType !== null;
  const isActive = hasTextQuery || hasFilter;

  // Derive display state from fetchResult vs current params
  const resultMatchesCurrent =
    fetchResult.forQuery === debouncedQuery &&
    fetchResult.forCraftType === craftType;
  const items = resultMatchesCurrent ? fetchResult.items : [];
  const totalCount = resultMatchesCurrent ? fetchResult.totalCount : 0;
  const error = resultMatchesCurrent ? fetchResult.error : null;

  const isFetching = isActive && (!resultMatchesCurrent || fetchResult.forPage !== page);
  const isLoadingMore = isFetching && resultMatchesCurrent && page > 1;

  // Fetch effect
  useEffect(() => {
    if (!isActive) return;

    abortControllerRef.current?.abort();
    const controller = new AbortController();
    abortControllerRef.current = controller;
    const isFirstPage = page === 1;

    searchItems(
      hasTextQuery ? debouncedQuery : null,
      page,
      DEFAULT_PAGE_SIZE,
      controller.signal,
      craftType ?? undefined,
    )
      .then((result) => {
        if (controller.signal.aborted) return;
        setFetchResult((prev) => ({
          items: isFirstPage
            ? result.items
            : [...prev.items, ...result.items],
          totalCount: result.totalCount,
          error: null,
          forQuery: debouncedQuery,
          forCraftType: craftType,
          forPage: page,
        }));
      })
      .catch((err: unknown) => {
        if (controller.signal.aborted) return;
        if (err instanceof DOMException && err.name === "AbortError") return;
        const msg =
          err instanceof ApiClientError
            ? err.message
            : "An unexpected error occurred. Please try again.";
        setFetchResult((prev) => ({
          ...prev,
          error: msg,
          forQuery: debouncedQuery,
          forCraftType: craftType,
          forPage: page,
        }));
      });

    return () => controller.abort();
  }, [debouncedQuery, craftType, page, retryCount, isActive, hasTextQuery]);

  const hasMore = items.length < totalCount;

  const loadMore = useCallback(() => {
    if (!isLoadingMore && hasMore) {
      setPage((p) => p + 1);
    }
  }, [isLoadingMore, hasMore]);

  const retry = useCallback(() => {
    setFetchResult((prev) => ({ ...prev, forQuery: "", forCraftType: null, forPage: 0 }));
    setRetryCount((c) => c + 1);
  }, []);

  // Show loading immediately when query changes (before debounce completes)
  const showLoading =
    isFetching ||
    (query.length >= MIN_SEARCH_LENGTH && query !== debouncedQuery);

  return {
    query,
    setQuery,
    craftType,
    setCraftType,
    items,
    totalCount,
    isLoading: showLoading,
    isLoadingMore,
    error,
    hasMore,
    loadMore,
    retry,
  };
}
