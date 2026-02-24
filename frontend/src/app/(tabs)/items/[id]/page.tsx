"use client";

import { useState, useEffect } from "react";
import { useParams, useRouter } from "next/navigation";
import { useBreadcrumbs } from "@/contexts/BreadcrumbContext";
import Link from "next/link";
import {
  ArrowLeft,
  Shield,
  Pickaxe,
  Store,
  FlaskConical,
  Sparkles,
  Map,
} from "lucide-react";
import { getItem, getItemContext } from "@/lib/api-client";
import { CRAFT_TYPE_INFO } from "@/lib/constants";
import {
  ItemIcon,
  StatBar,
  LoadingSpinner,
  ErrorState,
  SourceBadge,
} from "@/components/ui";
import type { ItemDto, CraftType } from "@/types/api";
import type { ItemContextDto } from "@/types/journey";

export default function ItemDetailPage() {
  const params = useParams<{ id: string }>();
  const router = useRouter();
  const itemId = Number(params.id);

  const [item, setItem] = useState<ItemDto | null>(null);
  const [context, setContext] = useState<ItemContextDto | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const { setBreadcrumbs } = useBreadcrumbs();

  useEffect(() => {
    if (isNaN(itemId)) return;

    setIsLoading(true);
    Promise.all([getItem(itemId), getItemContext(itemId)])
      .then(([itemData, contextData]) => {
        setItem(itemData);
        setContext(contextData);
      })
      .catch((err: unknown) => {
        setError(err instanceof Error ? err.message : "Failed to load item");
      })
      .finally(() => setIsLoading(false));
  }, [itemId]);

  useEffect(() => {
    if (item) {
      setBreadcrumbs([
        { label: "Items" },
        { label: item.name },
      ]);
    }
  }, [item, setBreadcrumbs]);

  if (isNaN(itemId)) {
    return (
      <div className="p-4">
        <ErrorState message="Invalid item ID" />
      </div>
    );
  }

  if (isLoading) {
    return (
      <div className="flex items-center justify-center p-12">
        <LoadingSpinner size={32} />
      </div>
    );
  }

  if (error || !item) {
    return (
      <div className="p-4">
        <ErrorState
          message={error ?? "Item not found"}
          onRetry={() => router.refresh()}
        />
      </div>
    );
  }

  const equipInfo = context?.equipmentInfo;

  return (
    <div className="p-4 space-y-6 max-w-2xl mx-auto">
      <button
        onClick={() => router.back()}
        className="flex items-center gap-1 text-text-secondary hover:text-gold transition-colors text-sm"
      >
        <ArrowLeft size={16} />
        Back
      </button>

      {/* Header */}
      <div className="flex items-start gap-4">
        <ItemIcon iconUrl={item.iconUrl} alt={item.name} size="2xl" />
        <div className="flex-1 min-w-0">
          <h1 className="text-xl font-bold text-text-primary">{item.name}</h1>
          <div className="flex items-center gap-2 mt-1 flex-wrap">
            <span className="text-sm text-text-muted">iLv {item.itemLevel}</span>
            <SourceBadge sourceType={item.category} />
          </div>
        </div>
      </div>

      {/* Recipes that produce this item */}
      {context && context.recipes.length > 0 && (
        <div className="bg-bg-surface rounded-lg p-4 border border-border space-y-3">
          <h2 className="text-sm font-semibold text-text-secondary uppercase tracking-wider">
            Recipes ({context.recipes.length})
          </h2>
          <div className="space-y-2">
            {context.recipes.map((recipe) => {
              const craftInfo = CRAFT_TYPE_INFO[recipe.craftType as CraftType];
              return (
                <div
                  key={recipe.recipeId}
                  className="flex items-center gap-3 p-3 rounded-lg bg-bg-elevated/50"
                >
                  <div className="flex-1 min-w-0">
                    <span className={`text-sm font-medium ${craftInfo?.colorClass ?? "text-text-primary"}`}>
                      {craftInfo?.fullName ?? recipe.craftType}
                    </span>
                    <span className="text-sm text-text-muted ml-2">
                      Lv. {recipe.level}
                      {recipe.stars > 0 && ` ${"★".repeat(recipe.stars)}`}
                    </span>
                  </div>
                  <div className="flex items-center gap-2">
                    <Link
                      href={`/recipes/${recipe.recipeId}`}
                      className="px-3 py-1.5 text-xs font-medium rounded-md bg-bg-elevated text-text-secondary hover:text-gold transition-colors"
                    >
                      Recipe
                    </Link>
                    <Link
                      href={`/recipes/${recipe.recipeId}/journey`}
                      className="px-3 py-1.5 text-xs font-medium rounded-md bg-bg-elevated text-text-secondary hover:text-gold transition-colors"
                    >
                      <span className="flex items-center gap-1">
                        <Map size={12} /> Journey
                      </span>
                    </Link>
                    <Link
                      href={`/simulator?recipeId=${recipe.recipeId}`}
                      className="px-3 py-1.5 text-xs font-medium rounded-md bg-bg-elevated text-text-secondary hover:text-gold transition-colors"
                    >
                      <span className="flex items-center gap-1">
                        <FlaskConical size={12} /> Simulate
                      </span>
                    </Link>
                  </div>
                </div>
              );
            })}
          </div>
        </div>
      )}

      {/* Used In Recipes */}
      {context && context.usedInRecipes.length > 0 && (
        <div className="bg-bg-surface rounded-lg p-4 border border-border space-y-3">
          <h2 className="text-sm font-semibold text-text-secondary uppercase tracking-wider">
            Used In ({context.usedInRecipes.length})
          </h2>
          <div className="space-y-2">
            {context.usedInRecipes.map((recipe) => {
              const craftInfo = CRAFT_TYPE_INFO[recipe.craftType as CraftType];
              return (
                <Link
                  key={recipe.recipeId}
                  href={`/recipes/${recipe.recipeId}`}
                  className="flex items-center gap-3 p-2 rounded-lg hover:bg-bg-elevated transition-colors group"
                >
                  <ItemIcon iconUrl={recipe.iconUrl} alt={recipe.resultItemName} size="sm" />
                  <span className="flex-1 text-sm text-text-primary group-hover:text-gold transition-colors truncate">
                    {recipe.resultItemName}
                  </span>
                  <span className={`text-xs ${craftInfo?.colorClass ?? "text-text-muted"}`}>
                    {craftInfo?.name ?? recipe.craftType} Lv.{recipe.level}
                  </span>
                </Link>
              );
            })}
          </div>
        </div>
      )}

      {/* Sources */}
      {context && (context.gatheringInfo.length > 0 || context.vendorSources.length > 0) && (
        <div className="bg-bg-surface rounded-lg p-4 border border-border space-y-4">
          <h2 className="text-sm font-semibold text-text-secondary uppercase tracking-wider">
            Sources
          </h2>

          {context.gatheringInfo.length > 0 && (
            <div className="space-y-2">
              <div className="flex items-center gap-2">
                <Pickaxe size={14} className="text-emerald-400" />
                <span className="text-xs font-semibold text-text-muted uppercase">Gathering</span>
              </div>
              {context.gatheringInfo.map((g, i) => (
                <div key={i} className="ml-5 text-sm space-y-0.5">
                  <div className="text-text-primary">
                    {g.zone}
                    {g.coordinates && (
                      <span className="text-text-muted ml-1">({g.coordinates})</span>
                    )}
                  </div>
                  <div className="text-xs text-text-muted">
                    {g.gatheringClass} Lv.{g.gatheringLevel} &middot; {g.gatheringType}
                    {g.isTimed && g.spawnTimes && (
                      <span className="text-amber-400 ml-1">Timed: {g.spawnTimes}</span>
                    )}
                  </div>
                </div>
              ))}
            </div>
          )}

          {context.vendorSources.length > 0 && (
            <div className="space-y-2">
              <div className="flex items-center gap-2">
                <Store size={14} className="text-amber-400" />
                <span className="text-xs font-semibold text-text-muted uppercase">Vendors</span>
              </div>
              {context.vendorSources.map((v, i) => (
                <div key={i} className="ml-5 text-sm">
                  <span className="text-text-primary">{v.npcName}</span>
                  <span className="text-text-muted ml-1">({v.zone})</span>
                  <span className="text-amber-400 ml-2 font-mono">
                    {v.cost.toLocaleString()} {v.currency}
                  </span>
                </div>
              ))}
            </div>
          )}
        </div>
      )}

      {/* Equipment stats */}
      {equipInfo && (
        <div className="rounded-lg border border-gold/30 bg-gold/5 p-4 space-y-3">
          <div className="flex items-center gap-2">
            <Shield size={18} className="text-gold" />
            <span className="text-sm font-medium text-gold">
              Equipment &middot; {equipInfo.equipSlot}
            </span>
          </div>
          <div className="grid grid-cols-3 gap-2">
            {equipInfo.craftsmanship > 0 && (
              <StatBar label="Craftsmanship" value={equipInfo.craftsmanship} />
            )}
            {equipInfo.control > 0 && (
              <StatBar label="Control" value={equipInfo.control} />
            )}
            {equipInfo.cp > 0 && (
              <StatBar label="CP" value={equipInfo.cp} />
            )}
            {equipInfo.gathering > 0 && (
              <StatBar label="Gathering" value={equipInfo.gathering} />
            )}
            {equipInfo.perception > 0 && (
              <StatBar label="Perception" value={equipInfo.perception} />
            )}
            {equipInfo.gp > 0 && (
              <StatBar label="GP" value={equipInfo.gp} />
            )}
          </div>
        </div>
      )}

      {/* Quick actions */}
      {context && context.canSimulate && context.recipes.length > 0 && (
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
          <Link
            href={`/simulator?recipeId=${context.recipes[0].recipeId}`}
            className="flex items-center gap-2 p-3 rounded-lg bg-bg-surface border border-border hover:border-gold transition-colors"
          >
            <FlaskConical className="text-gold shrink-0" size={18} />
            <span className="text-sm text-text-primary">Simulate</span>
          </Link>
          <Link
            href={`/simulator?recipeId=${context.recipes[0].recipeId}&solve=true`}
            className="flex items-center gap-2 p-3 rounded-lg bg-bg-surface border border-border hover:border-gold transition-colors"
          >
            <Sparkles className="text-gold shrink-0" size={18} />
            <span className="text-sm text-text-primary">Auto-Solve</span>
          </Link>
        </div>
      )}
    </div>
  );
}
