import { useState, useEffect, useCallback } from "react";
import { ApiClientError } from "@/lib/api-client";
import { getWorldList, getRecipeCost } from "@/lib/game-data";
import { MARKET_CONSTANTS } from "@/lib/constants";
import type { WorldGroupDto, CostEstimateDto } from "@/types/market";

// --- World list hook ---

interface UseWorldListReturn {
  worlds: WorldGroupDto[];
  selectedWorld: string;
  setSelectedWorld: (world: string) => void;
  isLoading: boolean;
}

export function useWorldList(): UseWorldListReturn {
  const [worlds, setWorlds] = useState<WorldGroupDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [selectedWorld, setSelectedWorldState] = useState<string>(() => {
    if (typeof window === "undefined") return "";
    try {
      return localStorage.getItem(MARKET_CONSTANTS.DEFAULT_WORLD_KEY) ?? "";
    } catch {
      return "";
    }
  });

  useEffect(() => {
    getWorldList()
      .then(setWorlds)
      .catch(() => {})
      .finally(() => setIsLoading(false));
  }, []);

  const setSelectedWorld = useCallback((world: string) => {
    setSelectedWorldState(world);
    try {
      localStorage.setItem(MARKET_CONSTANTS.DEFAULT_WORLD_KEY, world);
    } catch {
      // Storage blocked or full: the world is kept only until the page closes.
    }
  }, []);

  return { worlds, selectedWorld, setSelectedWorld, isLoading };
}

// --- Module-level cost estimate cache (15-min TTL) ---

interface CacheEntry {
  data: CostEstimateDto;
  expiresAt: number;
}

const costCache = new Map<string, CacheEntry>();
const COST_CACHE_TTL_MS = 15 * 60 * 1000;

function getCostCacheKey(recipeId: number, world: string, quantity: number, useMinPrice: boolean): string {
  return `${recipeId}:${world}:${quantity}:${useMinPrice}`;
}

// --- Recipe cost hook ---

export interface CostOptions {
  useMinPrice?: boolean;
  includeGatheredItemPrices?: boolean;
}

interface UseRecipeCostReturn {
  cost: CostEstimateDto | null;
  isLoading: boolean;
  error: string | null;
  fetchCost: (world: string) => void;
}

export function useRecipeCost(
  recipeId: number,
  quantity = 1,
  options?: CostOptions,
): UseRecipeCostReturn {
  const [cost, setCost] = useState<CostEstimateDto | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const fetchCost = useCallback(
    (world: string) => {
      if (!world || isNaN(recipeId)) return;

      const useMin = options?.useMinPrice ?? false;
      const cacheKey = getCostCacheKey(recipeId, world, quantity, useMin);
      const cached = costCache.get(cacheKey);
      if (cached && cached.expiresAt > Date.now()) {
        setCost(cached.data);
        return;
      }

      setIsLoading(true);
      setError(null);

      getRecipeCost(recipeId, world, quantity, options)
        .then((result) => {
          setCost(result);
          costCache.set(cacheKey, { data: result, expiresAt: Date.now() + COST_CACHE_TTL_MS });
          setIsLoading(false);
        })
        .catch((err: unknown) => {
          const msg =
            err instanceof ApiClientError
              ? err.message
              : "Failed to fetch cost estimate.";
          setError(msg);
          setIsLoading(false);
        });
    },
    [recipeId, quantity, options],
  );

  return { cost, isLoading, error, fetchCost };
}
