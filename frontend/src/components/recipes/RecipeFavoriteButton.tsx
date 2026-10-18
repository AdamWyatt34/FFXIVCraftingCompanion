"use client";

import { useState } from "react";
import { Heart } from "lucide-react";
import { useFavorites } from "@/hooks/useFavorites";
import { ProtectedAction } from "@/components/ui/ProtectedAction";

/** The favourite toggle in the recipe header; the only account-backed control on the page. */
export function RecipeFavoriteButton({ recipeId }: { recipeId: number }) {
  const { isFavorited, toggleFavorite } = useFavorites();
  const [animating, setAnimating] = useState(false);
  const favorited = isFavorited(recipeId);

  return (
    <ProtectedAction message="Sign in to favorite recipes">
      <button
        onClick={async () => {
          setAnimating(true);
          await toggleFavorite(recipeId);
          setTimeout(() => setAnimating(false), 300);
        }}
        className={`p-1.5 rounded-md transition-all duration-200 ${
          favorited ? "text-red-400 hover:text-red-300" : "text-text-muted hover:text-red-400"
        } ${animating ? "scale-125" : "scale-100"}`}
        aria-label={favorited ? "Remove from favorites" : "Add to favorites"}
      >
        <Heart size={20} fill={favorited ? "currentColor" : "none"} />
      </button>
    </ProtectedAction>
  );
}
