"use client";

import { useState, useRef, useEffect, useCallback } from "react";
import { Search, X, SlidersHorizontal } from "lucide-react";
import { useItemSearch } from "@/hooks/useItemSearch";
import { ItemIcon } from "@/components/ui/ItemIcon";
import { LoadingSpinner } from "@/components/ui/LoadingSpinner";
import { CRAFT_TYPE_INFO, ROTATION_SORT_OPTIONS } from "@/lib/constants";
import type { RotationSearchParams } from "@/types/solver";
import type { CraftType } from "@/types/api";

interface RotationFiltersProps {
  filters: RotationSearchParams;
  onFiltersChange: (update: Partial<RotationSearchParams>) => void;
}

const LEVEL_PRESETS = [90, 100] as const;
const CRAFT_TYPES = Object.entries(CRAFT_TYPE_INFO) as [
  CraftType,
  (typeof CRAFT_TYPE_INFO)[CraftType],
][];

export function RotationFilters({
  filters,
  onFiltersChange,
}: RotationFiltersProps) {
  const [showAdvanced, setShowAdvanced] = useState(false);
  const [selectedRecipeName, setSelectedRecipeName] = useState<string | null>(
    null,
  );

  // Recipe search
  const { query, setQuery, items, isLoading } = useItemSearch();
  const [isDropdownOpen, setIsDropdownOpen] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);

  const craftableItems = items.filter((item) => item.recipeId !== null);

  useEffect(() => {
    if (query.length < 2) return;
    const id = requestAnimationFrame(() => setIsDropdownOpen(true));
    return () => cancelAnimationFrame(id);
  }, [query]);

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

  const handleSelectRecipe = useCallback(
    (item: { recipeId: number | null; name: string }) => {
      if (!item.recipeId) return;
      onFiltersChange({ recipeId: item.recipeId });
      setSelectedRecipeName(item.name);
      setQuery("");
      setIsDropdownOpen(false);
    },
    [onFiltersChange, setQuery],
  );

  const handleClearRecipe = useCallback(() => {
    onFiltersChange({ recipeId: undefined });
    setSelectedRecipeName(null);
  }, [onFiltersChange]);

  const handleLevelPreset = useCallback(
    (level: number) => {
      const isActive =
        filters.minLevel === level && filters.maxLevel === level;
      if (isActive) {
        onFiltersChange({ minLevel: undefined, maxLevel: undefined });
      } else {
        onFiltersChange({ minLevel: level, maxLevel: level });
      }
    },
    [filters.minLevel, filters.maxLevel, onFiltersChange],
  );

  const hasActiveFilters =
    filters.recipeId ||
    filters.craftType ||
    filters.minLevel ||
    filters.maxLevel ||
    filters.source;

  return (
    <div className="space-y-3">
      {/* Top row: Sort + Recipe search */}
      <div className="flex gap-2 flex-wrap">
        {/* Sort dropdown */}
        <select
          value={filters.sortBy ?? "recent"}
          onChange={(e) =>
            onFiltersChange({
              sortBy: e.target.value as RotationSearchParams["sortBy"],
            })
          }
          className="bg-bg-elevated border border-border rounded-lg px-3 py-2 text-sm text-text-primary focus:outline-none focus:border-gold/50"
        >
          {ROTATION_SORT_OPTIONS.map((opt) => (
            <option key={opt.value} value={opt.value}>
              {opt.label}
            </option>
          ))}
        </select>

        {/* Recipe search / selected recipe chip */}
        <div ref={containerRef} className="relative flex-1 min-w-[200px] max-w-lg">
          {selectedRecipeName && filters.recipeId ? (
            <div className="flex items-center gap-2 bg-bg-elevated border border-gold/30 rounded-lg px-3 py-2">
              <span className="text-sm text-text-primary truncate flex-1">
                {selectedRecipeName}
              </span>
              <button
                onClick={handleClearRecipe}
                className="text-text-muted hover:text-text-primary transition-colors"
              >
                <X size={14} />
              </button>
            </div>
          ) : (
            <>
              <div className="relative">
                <Search
                  size={16}
                  className="absolute left-3 top-1/2 -translate-y-1/2 text-text-muted"
                />
                <input
                  type="text"
                  value={query}
                  onChange={(e) => setQuery(e.target.value)}
                  placeholder="Filter by recipe..."
                  className="w-full bg-bg-elevated border border-border rounded-lg pl-9 pr-3 py-2 text-sm text-text-primary placeholder:text-text-muted focus:outline-none focus:border-gold/50"
                />
                {isLoading && (
                  <div className="absolute right-3 top-1/2 -translate-y-1/2">
                    <LoadingSpinner size={14} />
                  </div>
                )}
              </div>

              {isDropdownOpen && query.length >= 2 && (
                <div className="absolute top-full left-0 right-0 mt-1 bg-bg-surface rounded-lg border border-border shadow-lg max-h-[240px] overflow-y-auto z-50">
                  {isLoading && craftableItems.length === 0 ? (
                    <div className="flex items-center justify-center py-4">
                      <LoadingSpinner size={20} />
                    </div>
                  ) : craftableItems.length === 0 ? (
                    <div className="px-4 py-4 text-center text-text-muted text-sm">
                      No recipes found
                    </div>
                  ) : (
                    craftableItems.slice(0, 10).map((item) => (
                      <button
                        key={item.itemId}
                        onClick={() => handleSelectRecipe(item)}
                        className="w-full flex items-center gap-2 px-3 py-2 hover:bg-bg-elevated cursor-pointer transition-colors text-left"
                      >
                        <ItemIcon
                          iconUrl={item.iconUrl}
                          alt={item.name}
                          size="sm"
                        />
                        <span className="text-sm text-text-primary truncate flex-1">
                          {item.name}
                        </span>
                        <span className="text-xs text-text-muted shrink-0">
                          iLv.{item.itemLevel}
                        </span>
                      </button>
                    ))
                  )}
                </div>
              )}
            </>
          )}
        </div>

        {/* Toggle advanced filters */}
        <button
          onClick={() => setShowAdvanced(!showAdvanced)}
          className={`flex items-center gap-1.5 px-3 py-2 rounded-lg border text-sm font-medium transition-colors ${
            showAdvanced || hasActiveFilters
              ? "bg-gold/10 border-gold/30 text-gold"
              : "bg-bg-elevated border-border text-text-secondary hover:text-text-primary"
          }`}
        >
          <SlidersHorizontal size={14} />
          Filters
          {hasActiveFilters && (
            <span className="w-1.5 h-1.5 rounded-full bg-gold" />
          )}
        </button>
      </div>

      {/* Advanced filters row */}
      {showAdvanced && (
        <div className="flex gap-2 flex-wrap items-center animate-fade-in">
          {/* CraftType pills */}
          <div className="flex gap-1 flex-wrap">
            {CRAFT_TYPES.map(([key, info]) => (
              <button
                key={key}
                onClick={() =>
                  onFiltersChange({
                    craftType: filters.craftType === key ? undefined : key,
                  })
                }
                className={`px-2 py-1 rounded text-xs font-medium transition-colors ${
                  filters.craftType === key
                    ? `bg-bg-elevated ${info.colorClass} border border-current`
                    : "bg-bg-elevated text-text-muted border border-transparent hover:text-text-secondary"
                }`}
              >
                {info.name}
              </button>
            ))}
          </div>

          <span className="text-border">|</span>

          {/* Level presets */}
          <div className="flex gap-1">
            {LEVEL_PRESETS.map((level) => {
              const isActive =
                filters.minLevel === level && filters.maxLevel === level;
              return (
                <button
                  key={level}
                  onClick={() => handleLevelPreset(level)}
                  className={`px-2 py-1 rounded text-xs font-medium transition-colors ${
                    isActive
                      ? "bg-gold/20 text-gold border border-gold/30"
                      : "bg-bg-elevated text-text-muted border border-transparent hover:text-text-secondary"
                  }`}
                >
                  Lv.{level}
                </button>
              );
            })}
          </div>

          <span className="text-border">|</span>

          {/* Source filter */}
          <div className="flex gap-1">
            {(["Solver", "Community", "User"] as const).map((source) => (
              <button
                key={source}
                onClick={() =>
                  onFiltersChange({
                    source: filters.source === source ? undefined : source,
                  })
                }
                className={`px-2 py-1 rounded text-xs font-medium transition-colors ${
                  filters.source === source
                    ? "bg-gold/20 text-gold border border-gold/30"
                    : "bg-bg-elevated text-text-muted border border-transparent hover:text-text-secondary"
                }`}
              >
                {source}
              </button>
            ))}
          </div>

          {/* Clear all */}
          {hasActiveFilters && (
            <>
              <span className="text-border">|</span>
              <button
                onClick={() => {
                  onFiltersChange({
                    recipeId: undefined,
                    craftType: undefined,
                    minLevel: undefined,
                    maxLevel: undefined,
                    source: undefined,
                  });
                  setSelectedRecipeName(null);
                }}
                className="text-xs text-red-400 hover:text-red-300 transition-colors"
              >
                Clear All
              </button>
            </>
          )}
        </div>
      )}
    </div>
  );
}
