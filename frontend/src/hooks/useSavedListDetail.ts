"use client";

import { useState, useEffect, useCallback } from "react";
import {
  getSavedListById,
  updateSavedList,
  deleteSavedList,
  addItemToSavedList,
  removeItemFromSavedList,
} from "@/lib/api-client";
import type { SavedListDto } from "@/types/saved-lists";

export function useSavedListDetail(id: number) {
  const [list, setList] = useState<SavedListDto | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const fetchList = useCallback(async () => {
    setIsLoading(true);
    setError(null);
    try {
      const data = await getSavedListById(id);
      setList(data);
    } catch {
      setError("Failed to load saved list.");
    } finally {
      setIsLoading(false);
    }
  }, [id]);

  useEffect(() => {
    fetchList();
  }, [fetchList]);

  const togglePin = useCallback(async () => {
    if (!list) return;
    try {
      const updated = await updateSavedList(list.id, { isPinned: !list.isPinned });
      setList(updated);
    } catch {
      setError("Failed to update pin status.");
    }
  }, [list]);

  const deleteListAction = useCallback(async () => {
    if (!list) return false;
    try {
      await deleteSavedList(list.id);
      return true;
    } catch {
      setError("Failed to delete list.");
      return false;
    }
  }, [list]);

  const addItem = useCallback(async (
    itemId: number,
    itemName: string,
    quantity: number,
    category?: string,
  ) => {
    if (!list) return;
    try {
      const updated = await addItemToSavedList(list.id, { itemId, itemName, quantity, category });
      setList(updated);
    } catch {
      setError("Failed to add item.");
    }
  }, [list]);

  const removeItem = useCallback(async (itemId: number) => {
    if (!list) return;
    try {
      const updated = await removeItemFromSavedList(list.id, itemId);
      setList(updated);
    } catch {
      setError("Failed to remove item.");
    }
  }, [list]);

  return {
    list,
    isLoading,
    error,
    togglePin,
    deleteList: deleteListAction,
    addItem,
    removeItem,
    refetch: fetchList,
  };
}
