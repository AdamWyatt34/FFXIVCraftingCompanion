"use client";

import { useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { Heart, Map } from "lucide-react";
import { ItemIcon } from "@/components/ui/ItemIcon";
import { LoadingSpinner } from "@/components/ui/LoadingSpinner";
import { useFavorites } from "@/hooks/useFavorites";

export function FavoriteRecipesList() {
  const router = useRouter();
  const { favorites, isLoading, error, toggleFavorite } = useFavorites();
  const [togglingId, setTogglingId] = useState<number | null>(null);

  async function handleUnfavorite(recipeId: number) {
    setTogglingId(recipeId);
    try {
      await toggleFavorite(recipeId);
    } finally {
      setTogglingId(null);
    }
  }

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-6">
        <LoadingSpinner size={24} />
      </div>
    );
  }

  if (error) {
    return (
      <div className="p-3 rounded-lg bg-red-500/10 border border-red-500/30 text-red-400 text-sm text-center">
        {error}
      </div>
    );
  }

  if (favorites.length === 0) {
    return (
      <div className="text-center py-6 text-text-muted text-sm">
        <Heart className="mx-auto mb-2 opacity-40" size={24} />
        No favorite recipes yet. Browse recipes and tap the heart to save them.
      </div>
    );
  }

  return (
    <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
      {favorites.map((fav) => (
        <div
          key={fav.recipeId}
          role="button"
          tabIndex={0}
          onClick={() => router.push(`/recipes/${fav.recipeId}`)}
          onKeyDown={(e) => { if (e.key === "Enter") router.push(`/recipes/${fav.recipeId}`); }}
          className="flex items-center gap-3 p-3 bg-bg-surface rounded-lg border border-border hover:border-border-focus transition-colors cursor-pointer"
        >
          <ItemIcon iconUrl={fav.iconUrl} alt={fav.itemName} size="lg" />
          <div className="flex-1 min-w-0">
            <span className="text-sm font-medium text-text-primary truncate block">
              {fav.itemName}
            </span>
            <div className="flex items-center gap-2 text-xs text-text-muted">
              <span>{fav.craftType}</span>
              <span>Lv. {fav.recipeLevel}</span>
            </div>
          </div>
          <Link
            href={`/recipes/${fav.recipeId}/journey`}
            onClick={(e) => e.stopPropagation()}
            className="p-1.5 rounded text-text-muted hover:text-gold transition-colors shrink-0"
            aria-label="Start journey"
          >
            <Map size={14} />
          </Link>
          <button
            onClick={(e) => {
              e.stopPropagation();
              handleUnfavorite(fav.recipeId);
            }}
            disabled={togglingId === fav.recipeId}
            className="p-1.5 rounded text-red-400 hover:text-red-300 transition-colors shrink-0 disabled:opacity-50"
            aria-label="Remove from favorites"
          >
            {togglingId === fav.recipeId ? (
              <LoadingSpinner size={14} />
            ) : (
              <Heart size={14} fill="currentColor" />
            )}
          </button>
        </div>
      ))}
    </div>
  );
}
