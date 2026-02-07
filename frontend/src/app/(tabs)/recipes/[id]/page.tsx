"use client";

import { useState, useEffect } from "react";
import { useRouter, useParams } from "next/navigation";
import Link from "next/link";
import { ArrowLeft, Sparkles } from "lucide-react";
import { getRecipe } from "@/lib/api-client";
import { CRAFT_TYPE_INFO } from "@/lib/constants";
import {
  ItemIcon,
  StatBar,
  LoadingSpinner,
  ErrorState,
} from "@/components/ui";
import type { RecipeDto, CraftType } from "@/types/api";

export default function RecipeDetailPage() {
  const params = useParams<{ id: string }>();
  const router = useRouter();
  const [recipe, setRecipe] = useState<RecipeDto | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const id = Number(params.id);
    if (isNaN(id)) {
      setError("Invalid recipe ID");
      setIsLoading(false);
      return;
    }

    setIsLoading(true);
    setError(null);

    getRecipe(id)
      .then(setRecipe)
      .catch((err: unknown) => {
        setError(
          err instanceof Error ? err.message : "Failed to load recipe",
        );
      })
      .finally(() => setIsLoading(false));
  }, [params.id]);

  if (isLoading) {
    return (
      <div className="flex items-center justify-center p-12">
        <LoadingSpinner size={32} />
      </div>
    );
  }

  if (error || !recipe) {
    return (
      <div className="p-4">
        <ErrorState
          message={error ?? "Recipe not found"}
          onRetry={() => router.refresh()}
        />
      </div>
    );
  }

  const craftInfo = CRAFT_TYPE_INFO[recipe.craftType as CraftType];

  return (
    <div className="p-4 space-y-6 max-w-2xl mx-auto">
      {/* Back navigation */}
      <button
        onClick={() => router.back()}
        className="flex items-center gap-1 text-text-secondary hover:text-gold transition-colors text-sm"
      >
        <ArrowLeft size={16} />
        Back to search
      </button>

      {/* Result item header */}
      <div className="flex items-center gap-4">
        <ItemIcon
          iconUrl={recipe.resultItem.iconUrl}
          alt={recipe.resultItem.name}
          size="xl"
        />
        <div>
          <h1 className="text-xl font-bold text-text-primary">
            {recipe.resultItem.name}
          </h1>
          <div className="flex items-center gap-2 mt-1">
            <span className={`text-sm font-medium ${craftInfo.colorClass}`}>
              {craftInfo.fullName}
            </span>
            <span className="text-text-muted text-sm">
              Lv. {recipe.recipeLevel}
            </span>
          </div>
        </div>
      </div>

      {/* Recipe stats */}
      <div className="bg-bg-surface rounded-lg p-4 space-y-3 border border-border">
        <h2 className="text-sm font-semibold text-text-secondary uppercase tracking-wider">
          Recipe Stats
        </h2>
        <StatBar label="Difficulty" value={recipe.difficulty} />
        <StatBar label="Quality" value={recipe.quality} />
        <StatBar label="Durability" value={recipe.durability} />
      </div>

      {/* Required stats */}
      <div className="bg-bg-surface rounded-lg p-4 space-y-3 border border-border">
        <h2 className="text-sm font-semibold text-text-secondary uppercase tracking-wider">
          Required Stats
        </h2>
        <StatBar label="Craftsmanship" value={recipe.requiredCraftsmanship} />
        <StatBar label="Control" value={recipe.requiredControl} />
      </div>

      {/* Ingredients */}
      <div className="bg-bg-surface rounded-lg p-4 border border-border">
        <h2 className="text-sm font-semibold text-text-secondary uppercase tracking-wider mb-3">
          Ingredients ({recipe.ingredients.length})
        </h2>
        <div className="space-y-2">
          {recipe.ingredients.map((ingredient) => (
            <Link
              key={ingredient.itemId}
              href={`/recipes?q=${encodeURIComponent(ingredient.itemName)}`}
              className="flex items-center gap-3 p-2 rounded-lg hover:bg-bg-elevated transition-colors group"
            >
              <ItemIcon
                iconUrl={ingredient.iconUrl}
                alt={ingredient.itemName}
                size="md"
              />
              <span className="flex-1 text-sm text-text-primary group-hover:text-gold transition-colors truncate">
                {ingredient.itemName}
              </span>
              <div className="flex items-center gap-2 shrink-0">
                {ingredient.canBeHQ && (
                  <span title="Can be HQ">
                    <Sparkles size={14} className="text-gold" />
                  </span>
                )}
                <span className="text-sm text-text-muted font-mono">
                  x{ingredient.quantity}
                </span>
              </div>
            </Link>
          ))}
        </div>
      </div>
    </div>
  );
}
