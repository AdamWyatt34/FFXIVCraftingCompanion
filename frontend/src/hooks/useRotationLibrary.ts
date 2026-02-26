"use client";

import { useState, useEffect, useCallback, useRef } from "react";
import { searchRotations, ApiClientError } from "@/lib/api-client";
import { ROTATION_LIBRARY_CONSTANTS } from "@/lib/constants";
import type { RotationListItem, RotationSearchParams } from "@/types/solver";

export interface UseRotationLibraryReturn {
  rotations: RotationListItem[];
  totalCount: number;
  page: number;
  pageSize: number;
  isLoading: boolean;
  error: string | null;
  filters: RotationSearchParams;
  setFilters: (update: Partial<RotationSearchParams>) => void;
  loadMore: () => void;
  hasMore: boolean;
  retry: () => void;
}

export function useRotationLibrary(
  initialFilters?: Partial<RotationSearchParams>,
): UseRotationLibraryReturn {
  const [rotations, setRotations] = useState<RotationListItem[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [filters, setFiltersState] = useState<RotationSearchParams>({
    sortBy: "recent",
    page: 1,
    pageSize: ROTATION_LIBRARY_CONSTANTS.DEFAULT_PAGE_SIZE,
    ...initialFilters,
  });
  const abortRef = useRef<AbortController | null>(null);
  const isLoadingMoreRef = useRef(false);

  const fetchRotations = useCallback(
    async (currentFilters: RotationSearchParams, append: boolean) => {
      abortRef.current?.abort();
      const controller = new AbortController();
      abortRef.current = controller;

      setIsLoading(true);
      setError(null);

      try {
        const result = await searchRotations(currentFilters, controller.signal);
        if (controller.signal.aborted) return;

        if (append) {
          setRotations((prev) => [...prev, ...result.rotations]);
        } else {
          setRotations(result.rotations);
        }
        setTotalCount(result.totalCount);
      } catch (err) {
        if (err instanceof DOMException && err.name === "AbortError") return;
        setError(
          err instanceof ApiClientError
            ? err.body.error
            : "Failed to load rotations",
        );
      } finally {
        setIsLoading(false);
        isLoadingMoreRef.current = false;
      }
    },
    [],
  );

  // Fetch on filter change (reset to page 1)
  useEffect(() => {
    fetchRotations(filters, false);
    return () => {
      abortRef.current?.abort();
    };
  }, [filters, fetchRotations]);

  const setFilters = useCallback((update: Partial<RotationSearchParams>) => {
    setFiltersState((prev) => ({
      ...prev,
      ...update,
      page: 1, // Reset to first page on filter change
    }));
  }, []);

  const loadMoreFn = useCallback(() => {
    if (isLoading) return;
    const loaded = rotations.length;
    if (loaded >= totalCount) return;

    const nextPage = (filters.page ?? 1) + 1;
    const nextFilters = { ...filters, page: nextPage };

    // Fetch and append
    isLoadingMoreRef.current = true;
    abortRef.current?.abort();
    const controller = new AbortController();
    abortRef.current = controller;
    setIsLoading(true);

    searchRotations(nextFilters, controller.signal)
      .then((result) => {
        if (controller.signal.aborted) return;
        setRotations((prev) => [...prev, ...result.rotations]);
        setTotalCount(result.totalCount);
        setFiltersState((prev) => ({ ...prev, page: nextPage }));
      })
      .catch((err) => {
        if (err instanceof DOMException && err.name === "AbortError") return;
        setError(
          err instanceof ApiClientError
            ? err.body.error
            : "Failed to load more rotations",
        );
      })
      .finally(() => {
        setIsLoading(false);
        isLoadingMoreRef.current = false;
      });
  }, [filters, rotations.length, totalCount, isLoading]);

  const hasMore =
    rotations.length < totalCount;

  const retry = useCallback(() => {
    fetchRotations(filters, false);
  }, [filters, fetchRotations]);

  return {
    rotations,
    totalCount,
    page: filters.page ?? 1,
    pageSize: filters.pageSize ?? ROTATION_LIBRARY_CONSTANTS.DEFAULT_PAGE_SIZE,
    isLoading,
    error,
    filters,
    setFilters,
    loadMore: loadMoreFn,
    hasMore,
    retry,
  };
}
