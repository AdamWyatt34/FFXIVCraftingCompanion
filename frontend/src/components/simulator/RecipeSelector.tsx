"use client";

import { useState, useRef, useEffect, useCallback } from "react";
import { RefreshCw } from "lucide-react";
import { SearchBar } from "@/components/ui/SearchBar";
import { ItemIcon } from "@/components/ui/ItemIcon";
import { LoadingSpinner } from "@/components/ui/LoadingSpinner";
import { useItemSearch } from "@/hooks/useItemSearch";
import { ApiClientError } from "@/lib/api-client";
import { getRecipe } from "@/lib/game-data";
import { CRAFT_TYPE_INFO } from "@/lib/constants";
import type { RecipeDto, CraftType, ItemDto } from "@/types/api";

interface RecipeSelectorProps {
  selectedRecipe: RecipeDto | null;
  onRecipeSelected: (recipe: RecipeDto) => void;
}

export function RecipeSelector({
  selectedRecipe,
  onRecipeSelected,
}: RecipeSelectorProps) {
  const { query, setQuery, items, isLoading, error } = useItemSearch();
  const [isLoadingRecipe, setIsLoadingRecipe] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [isDropdownOpen, setIsDropdownOpen] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);

  // Filter to only craftable items
  const craftableItems = items.filter((item) => item.recipeId !== null);

  // Open dropdown when there are results
  useEffect(() => {
    if (query.length >= 2 && !selectedRecipe) {
      setIsDropdownOpen(true);
    }
  }, [query, selectedRecipe]);

  // Close dropdown on click outside
  useEffect(() => {
    function handleClickOutside(e: MouseEvent) {
      if (
        containerRef.current &&
        !containerRef.current.contains(e.target as Node)
      ) {
        setIsDropdownOpen(false);
      }
    }
    document.addEventListener("mousedown", handleClickOutside);
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, []);

  const handleSelect = useCallback(
    async (item: ItemDto) => {
      if (!item.recipeId) return;
      setIsLoadingRecipe(true);
      setLoadError(null);
      setIsDropdownOpen(false);

      try {
        const recipe = await getRecipe(item.recipeId);
        onRecipeSelected(recipe);
        setQuery("");
      } catch (err) {
        if (err instanceof ApiClientError) {
          setLoadError(err.body?.error ?? "Failed to load recipe details");
        } else {
          setLoadError("Failed to load recipe details");
        }
      } finally {
        setIsLoadingRecipe(false);
      }
    },
    [onRecipeSelected, setQuery],
  );

  const handleChange = () => {
    onRecipeSelected(null as unknown as RecipeDto);
    setQuery("");
    setLoadError(null);
  };

  // Selected recipe card
  if (selectedRecipe) {
    const craftInfo =
      CRAFT_TYPE_INFO[selectedRecipe.craftType as CraftType] ?? null;
    return (
      <div className="bg-bg-surface rounded-lg border border-border p-4 animate-fade-in">
        <div className="flex items-start gap-4">
          <ItemIcon
            iconUrl={selectedRecipe.resultItem.iconUrl}
            alt={selectedRecipe.resultItem.name}
            size="xl"
          />
          <div className="flex-1 min-w-0">
            <h3 className="font-bold text-text-primary truncate">
              {selectedRecipe.resultItem.name}
            </h3>
            <div className="flex items-center gap-2 mt-1">
              {craftInfo && (
                <span
                  className={`text-xs px-2 py-0.5 rounded-full bg-bg-elevated ${craftInfo.colorClass}`}
                >
                  {craftInfo.name}
                </span>
              )}
              <span className="text-sm text-text-secondary">
                Recipe Lv. {selectedRecipe.recipeLevel}
              </span>
            </div>
          </div>
          <button
            onClick={handleChange}
            className="text-sm text-text-secondary hover:text-gold transition-colors flex items-center gap-1 shrink-0"
          >
            <RefreshCw size={14} />
            Change
          </button>
        </div>

        <div className="grid grid-cols-3 gap-2 mt-4">
          <div className="bg-bg-elevated rounded-md p-2 text-center">
            <p className="text-lg font-bold text-text-primary">
              {selectedRecipe.difficulty.toLocaleString()}
            </p>
            <p className="text-xs text-text-muted">Difficulty</p>
          </div>
          <div className="bg-bg-elevated rounded-md p-2 text-center">
            <p className="text-lg font-bold text-text-primary">
              {selectedRecipe.quality.toLocaleString()}
            </p>
            <p className="text-xs text-text-muted">Quality</p>
          </div>
          <div className="bg-bg-elevated rounded-md p-2 text-center">
            <p className="text-lg font-bold text-text-primary">
              {selectedRecipe.durability}
            </p>
            <p className="text-xs text-text-muted">Durability</p>
          </div>
        </div>
      </div>
    );
  }

  // Search state
  return (
    <div ref={containerRef} className="relative">
      <SearchBar
        value={query}
        onChange={(v) => {
          setQuery(v);
          setLoadError(null);
        }}
        isLoading={isLoading || isLoadingRecipe}
        placeholder="Search for a recipe..."
      />

      {loadError && (
        <p className="text-xs text-red-400 mt-1">{loadError}</p>
      )}

      {error && (
        <p className="text-xs text-red-400 mt-1">{error}</p>
      )}

      {isDropdownOpen && query.length >= 2 && (
        <div className="absolute top-full left-0 right-0 mt-1 bg-bg-surface rounded-lg border border-border shadow-lg max-h-[300px] overflow-y-auto z-50">
          {isLoading && craftableItems.length === 0 ? (
            <div className="flex items-center justify-center py-6">
              <LoadingSpinner size={24} />
            </div>
          ) : craftableItems.length === 0 ? (
            <div className="px-4 py-6 text-center text-text-muted text-sm">
              No recipes found for &ldquo;{query}&rdquo;
            </div>
          ) : (
            craftableItems.map((item) => (
              <button
                key={item.itemId}
                onClick={() => handleSelect(item)}
                className="w-full flex items-center gap-3 px-3 py-2.5 hover:bg-bg-elevated cursor-pointer transition-colors text-left"
              >
                <ItemIcon
                  iconUrl={item.iconUrl}
                  alt={item.name}
                  size="md"
                />
                <div className="flex-1 min-w-0">
                  <p className="text-sm text-text-primary truncate">
                    {item.name}
                  </p>
                </div>
                <span className="text-xs text-text-muted shrink-0">
                  iLv. {item.itemLevel}
                </span>
              </button>
            ))
          )}
        </div>
      )}
    </div>
  );
}
