"use client";

import { createContext, useContext, useState, useCallback, useEffect } from "react";

export interface CraftPlanItem {
  recipeId: number;
  itemName: string;
  iconUrl: string;
  craftType: string;
  recipeLevel: number;
  quantity: number;
}

interface CraftPlanContextValue {
  planItems: CraftPlanItem[];
  addToPlan: (item: Omit<CraftPlanItem, "quantity">) => void;
  removeFromPlan: (recipeId: number) => void;
  updateQuantity: (recipeId: number, qty: number) => void;
  clearPlan: () => void;
  planItemCount: number;
}

const CraftPlanContext = createContext<CraftPlanContextValue | null>(null);

const STORAGE_KEY = "ffxiv_craft_plan";

function loadFromStorage(): CraftPlanItem[] {
  if (typeof window === "undefined") return [];
  try {
    const data = sessionStorage.getItem(STORAGE_KEY);
    return data ? (JSON.parse(data) as CraftPlanItem[]) : [];
  } catch {
    return [];
  }
}

function saveToStorage(items: CraftPlanItem[]) {
  if (typeof window === "undefined") return;
  try {
    sessionStorage.setItem(STORAGE_KEY, JSON.stringify(items));
  } catch {
    // ignore quota errors
  }
}

export function CraftPlanProvider({ children }: { children: React.ReactNode }) {
  const [planItems, setPlanItems] = useState<CraftPlanItem[]>(() => loadFromStorage());

  useEffect(() => {
    saveToStorage(planItems);
  }, [planItems]);

  const addToPlan = useCallback((item: Omit<CraftPlanItem, "quantity">) => {
    setPlanItems((prev) => {
      const existing = prev.find((p) => p.recipeId === item.recipeId);
      if (existing) {
        return prev.map((p) =>
          p.recipeId === item.recipeId
            ? { ...p, quantity: p.quantity + 1 }
            : p,
        );
      }
      return [...prev, { ...item, quantity: 1 }];
    });
  }, []);

  const removeFromPlan = useCallback((recipeId: number) => {
    setPlanItems((prev) => prev.filter((p) => p.recipeId !== recipeId));
  }, []);

  const updateQuantity = useCallback((recipeId: number, qty: number) => {
    if (qty < 1 || qty > 999) return;
    setPlanItems((prev) =>
      prev.map((p) => (p.recipeId === recipeId ? { ...p, quantity: qty } : p)),
    );
  }, []);

  const clearPlan = useCallback(() => {
    setPlanItems([]);
  }, []);

  return (
    <CraftPlanContext.Provider
      value={{
        planItems,
        addToPlan,
        removeFromPlan,
        updateQuantity,
        clearPlan,
        planItemCount: planItems.length,
      }}
    >
      {children}
    </CraftPlanContext.Provider>
  );
}

export function useCraftPlanContext(): CraftPlanContextValue {
  const ctx = useContext(CraftPlanContext);
  if (!ctx) throw new Error("useCraftPlanContext must be used within CraftPlanProvider");
  return ctx;
}
