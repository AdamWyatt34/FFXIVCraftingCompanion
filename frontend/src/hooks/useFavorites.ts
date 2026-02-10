"use client";

import { useState, useEffect, useCallback, useMemo, useRef } from "react";
import {
  getFavoriteRecipes,
  toggleFavoriteRecipe,
  ApiClientError,
} from "@/lib/api-client";
import { useAuth } from "@/contexts/AuthContext";
import type { FavoriteRecipeDto } from "@/types/saved-lists";

interface UseFavoritesReturn {
  favorites: FavoriteRecipeDto[];
  isLoading: boolean;
  error: string | null;
  retry: () => void;
  toggleFavorite: (recipeId: number, note?: string) => Promise<boolean>;
  isFavorited: (recipeId: number) => boolean;
}

export function useFavorites(): UseFavoritesReturn {
  const { isAuthenticated } = useAuth();
  const [favorites, setFavorites] = useState<FavoriteRecipeDto[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const abortRef = useRef<AbortController | null>(null);

  const fetchFavorites = useCallback(async () => {
    if (!isAuthenticated) return;

    abortRef.current?.abort();
    const controller = new AbortController();
    abortRef.current = controller;

    setIsLoading(true);
    setError(null);

    try {
      const data = await getFavoriteRecipes(controller.signal);
      setFavorites(data);
    } catch (err) {
      if (err instanceof DOMException && err.name === "AbortError") return;
      setError(err instanceof ApiClientError ? err.body.error : "Failed to load favorites");
    } finally {
      setIsLoading(false);
    }
  }, [isAuthenticated]);

  useEffect(() => {
    if (!isAuthenticated) {
      setFavorites([]);
      return;
    }
    fetchFavorites();
    return () => {
      abortRef.current?.abort();
    };
  }, [isAuthenticated, fetchFavorites]);

  const favoritedIds = useMemo(
    () => new Set(favorites.map((f) => f.recipeId)),
    [favorites],
  );

  const isFavorited = useCallback(
    (recipeId: number) => favoritedIds.has(recipeId),
    [favoritedIds],
  );

  const toggleFavoriteHandler = useCallback(
    async (recipeId: number, note?: string): Promise<boolean> => {
      const result = await toggleFavoriteRecipe(recipeId, note);
      if (result.isFavorited) {
        // Re-fetch to get full DTO
        fetchFavorites();
      } else {
        setFavorites((prev) => prev.filter((f) => f.recipeId !== recipeId));
      }
      return result.isFavorited;
    },
    [fetchFavorites],
  );

  return {
    favorites,
    isLoading,
    error,
    retry: fetchFavorites,
    toggleFavorite: toggleFavoriteHandler,
    isFavorited,
  };
}
