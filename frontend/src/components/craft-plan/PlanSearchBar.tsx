"use client";

import { Plus } from "lucide-react";
import { SearchBar, ItemIcon } from "@/components/ui";
import { useItemSearch } from "@/hooks/useItemSearch";
import { CRAFT_TYPE_INFO } from "@/lib/constants";
import type { CraftType } from "@/types/api";

interface PlanSearchBarProps {
  onAdd: (item: {
    recipeId: number;
    itemName: string;
    iconUrl: string;
    craftType: string;
    recipeLevel: number;
  }) => void;
  existingRecipeIds: Set<number>;
}

export function PlanSearchBar({ onAdd, existingRecipeIds }: PlanSearchBarProps) {
  const { query, setQuery, items, isLoading } = useItemSearch();

  // Filter to only craftable items
  const craftable = items.filter((i) => i.recipeId !== null);

  return (
    <div className="space-y-2">
      <SearchBar
        value={query}
        onChange={setQuery}
        placeholder="Search recipes to add..."
        isLoading={isLoading}
      />

      {craftable.length > 0 && query.length >= 2 && (
        <div className="bg-bg-surface rounded-lg border border-border max-h-60 overflow-y-auto">
          {craftable.map((item) => {
            const alreadyAdded = existingRecipeIds.has(item.recipeId!);
            return (
              <button
                key={item.itemId}
                onClick={() => {
                  if (alreadyAdded) return;
                  onAdd({
                    recipeId: item.recipeId!,
                    itemName: item.name,
                    iconUrl: item.iconUrl,
                    craftType: item.category === "Craftable" ? "CRP" : "CRP",
                    recipeLevel: item.itemLevel,
                  });
                }}
                disabled={alreadyAdded}
                className="w-full flex items-center gap-3 px-4 py-2.5 hover:bg-bg-elevated transition-colors disabled:opacity-50 min-h-[44px]"
              >
                <ItemIcon iconUrl={item.iconUrl} alt={item.name} size="sm" />
                <span className="flex-1 text-sm text-text-primary truncate text-left">
                  {item.name}
                </span>
                <span className="text-xs text-text-muted">iLv {item.itemLevel}</span>
                {alreadyAdded ? (
                  <span className="text-xs text-text-muted">Added</span>
                ) : (
                  <Plus size={16} className="text-gold shrink-0" />
                )}
              </button>
            );
          })}
        </div>
      )}
    </div>
  );
}
