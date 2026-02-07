import { useState, useEffect, useRef, useCallback } from "react";
import { useDebounce } from "./useDebounce";
import { searchItems, ApiClientError } from "@/lib/api-client";
import { DEFAULT_PAGE_SIZE, MIN_SEARCH_LENGTH } from "@/lib/constants";
import type { ItemDto } from "@/types/api";

interface UseItemSearchReturn {
  query: string;
  setQuery: (q: string) => void;
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
  const [query, setQuery] = useState(initialQuery);
  const [items, setItems] = useState<ItemDto[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [page, setPage] = useState(1);
  const [isLoading, setIsLoading] = useState(false);
  const [isLoadingMore, setIsLoadingMore] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const debouncedQuery = useDebounce(query, 300);
  const abortControllerRef = useRef<AbortController | null>(null);

  // Reset when debounced query changes
  useEffect(() => {
    setItems([]);
    setTotalCount(0);
    setPage(1);
    setError(null);
  }, [debouncedQuery]);

  // Fetch results
  useEffect(() => {
    if (debouncedQuery.length < MIN_SEARCH_LENGTH) {
      setIsLoading(false);
      return;
    }

    abortControllerRef.current?.abort();
    const controller = new AbortController();
    abortControllerRef.current = controller;

    const isFirstPage = page === 1;
    if (isFirstPage) {
      setIsLoading(true);
    } else {
      setIsLoadingMore(true);
    }

    searchItems(debouncedQuery, page, DEFAULT_PAGE_SIZE, controller.signal)
      .then((result) => {
        if (controller.signal.aborted) return;
        setItems((prev) =>
          isFirstPage ? result.items : [...prev, ...result.items],
        );
        setTotalCount(result.totalCount);
        setError(null);
      })
      .catch((err: unknown) => {
        if (controller.signal.aborted) return;
        if (err instanceof DOMException && err.name === "AbortError") return;
        if (err instanceof ApiClientError) {
          setError(err.message);
        } else {
          setError("An unexpected error occurred. Please try again.");
        }
      })
      .finally(() => {
        if (controller.signal.aborted) return;
        setIsLoading(false);
        setIsLoadingMore(false);
      });

    return () => {
      controller.abort();
    };
  }, [debouncedQuery, page]);

  const hasMore = items.length < totalCount;

  const loadMore = useCallback(() => {
    if (!isLoadingMore && hasMore) {
      setPage((p) => p + 1);
    }
  }, [isLoadingMore, hasMore]);

  const retry = useCallback(() => {
    setError(null);
    setPage(1);
    setItems([]);
  }, []);

  // Show loading immediately when query changes (before debounce completes)
  const showLoading =
    isLoading ||
    (query.length >= MIN_SEARCH_LENGTH && query !== debouncedQuery);

  return {
    query,
    setQuery,
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
