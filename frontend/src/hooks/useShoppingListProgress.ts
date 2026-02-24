"use client";

import { useState, useCallback } from "react";

const STORAGE_PREFIX = "ffxiv_shopping_progress:";

export interface UseShoppingListProgressReturn {
  checkedItems: Set<number>;
  toggle: (itemId: number) => void;
  isChecked: (itemId: number) => boolean;
  progress: number; // 0-1
  checkedCount: number;
  clear: () => void;
}

export function useShoppingListProgress(
  recipeId: number,
  totalItems: number,
): UseShoppingListProgressReturn {
  const storageKey = `${STORAGE_PREFIX}${recipeId}`;

  const [checkedItems, setCheckedItems] = useState<Set<number>>(() => {
    try {
      const raw = localStorage.getItem(storageKey);
      if (raw) return new Set(JSON.parse(raw) as number[]);
    } catch { /* ignore */ }
    return new Set();
  });

  const persist = useCallback(
    (ids: Set<number>) => {
      try {
        localStorage.setItem(storageKey, JSON.stringify([...ids]));
      } catch { /* ignore */ }
    },
    [storageKey],
  );

  const toggle = useCallback(
    (itemId: number) => {
      setCheckedItems((prev) => {
        const next = new Set(prev);
        if (next.has(itemId)) next.delete(itemId);
        else next.add(itemId);
        persist(next);
        return next;
      });
    },
    [persist],
  );

  const isChecked = useCallback(
    (itemId: number) => checkedItems.has(itemId),
    [checkedItems],
  );

  const clear = useCallback(() => {
    setCheckedItems(new Set());
    try {
      localStorage.removeItem(storageKey);
    } catch { /* ignore */ }
  }, [storageKey]);

  const checkedCount = checkedItems.size;
  const progress = totalItems > 0 ? checkedCount / totalItems : 0;

  return { checkedItems, toggle, isChecked, progress, checkedCount, clear };
}
