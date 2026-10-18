"use client";

import { useState, useEffect, useCallback, useRef } from "react";
import {
  getSavedLists,
  deleteSavedList as apiDeleteSavedList,
  updateSavedList,
  ApiClientError,
} from "@/lib/api-client";
import { useAuth } from "@/contexts/AuthContext";
import type { SavedListSummaryDto } from "@/types/saved-lists";

interface UseSavedListsReturn {
  lists: SavedListSummaryDto[];
  isLoading: boolean;
  error: string | null;
  retry: () => void;
  deleteList: (id: number) => Promise<void>;
  togglePin: (id: number, isPinned: boolean) => Promise<void>;
}

export function useSavedLists(typeFilter?: string): UseSavedListsReturn {
  const { isAuthenticated } = useAuth();
  const [lists, setLists] = useState<SavedListSummaryDto[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const abortRef = useRef<AbortController | null>(null);

  const fetchLists = useCallback(async () => {
    if (!isAuthenticated) return;

    abortRef.current?.abort();
    const controller = new AbortController();
    abortRef.current = controller;

    setIsLoading(true);
    setError(null);

    try {
      const data = await getSavedLists(typeFilter, controller.signal);
      const sorted = [...data].sort((a, b) => {
        if (a.isPinned !== b.isPinned) return a.isPinned ? -1 : 1;
        return new Date(b.updatedAt).getTime() - new Date(a.updatedAt).getTime();
      });
      setLists(sorted);
    } catch (err) {
      if (controller.signal.aborted) return;
      setError(err instanceof ApiClientError ? err.body.error : "Failed to load saved lists");
    } finally {
      setIsLoading(false);
    }
  }, [isAuthenticated, typeFilter]);

  useEffect(() => {
    if (!isAuthenticated) {
      setLists([]);
      return;
    }
    fetchLists();
    return () => {
      abortRef.current?.abort();
    };
  }, [isAuthenticated, fetchLists]);

  const deleteList = useCallback(async (id: number) => {
    await apiDeleteSavedList(id);
    setLists((prev) => prev.filter((l) => l.id !== id));
  }, []);

  const togglePin = useCallback(async (id: number, isPinned: boolean) => {
    await updateSavedList(id, { isPinned });
    setLists((prev) => {
      const updated = prev.map((l) => (l.id === id ? { ...l, isPinned } : l));
      return updated.sort((a, b) => {
        if (a.isPinned !== b.isPinned) return a.isPinned ? -1 : 1;
        return new Date(b.updatedAt).getTime() - new Date(a.updatedAt).getTime();
      });
    });
  }, []);

  return { lists, isLoading, error, retry: fetchLists, deleteList, togglePin };
}
