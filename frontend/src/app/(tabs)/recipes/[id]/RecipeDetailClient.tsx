"use client";

import { useState, useEffect, Suspense } from "react";
import { useRouter, useParams, useSearchParams } from "next/navigation";
import Link from "next/link";
import {
  ArrowLeft,
  Sparkles,
  ShoppingCart,
  TreePine,
  ListOrdered,
  Info,
  Shield,
  Heart,
  FlaskConical,
  Map,
} from "lucide-react";
import { getRecipe, trackAnalyticsEvent } from "@/lib/api-client";
import { CRAFT_TYPE_INFO } from "@/lib/constants";
import { useBreadcrumbs } from "@/contexts/BreadcrumbContext";
import {
  ItemIcon,
  ItemLink,
  StatBar,
  LoadingSpinner,
  ErrorState,
} from "@/components/ui";
import {
  MaterialTree,
  QuantityMultiplier,
  ShoppingListView,
  CraftingStepsView,
  ShoppingListSkeleton,
  CraftingStepsSkeleton,
  RecipeCraftabilityBanner,
} from "@/components/recipes";
import { useMaterialTree } from "@/hooks/useMaterialTree";
import { useFavorites } from "@/hooks/useFavorites";
import { useProfile } from "@/hooks/useProfile";
import { ProtectedAction } from "@/components/ui/ProtectedAction";
import type { RecipeDto, CraftType } from "@/types/api";

type TabKey = "shopping" | "tree" | "steps";

const TABS: { key: TabKey; label: string; icon: typeof ShoppingCart }[] = [
  { key: "shopping", label: "Shopping List", icon: ShoppingCart },
  { key: "tree", label: "Crafting Tree", icon: TreePine },
  { key: "steps", label: "Crafting Steps", icon: ListOrdered },
];

function isValidTab(val: string | null): val is TabKey {
  return val === "shopping" || val === "tree" || val === "steps";
}

function RecipeDetailContent() {
  const params = useParams<{ id: string }>();
  const router = useRouter();
  const searchParams = useSearchParams();
  const [recipe, setRecipe] = useState<RecipeDto | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [quantity, setQuantity] = useState(1);
  const [animatingFav, setAnimatingFav] = useState(false);
  const { isFavorited, toggleFavorite } = useFavorites();
  const { profile } = useProfile();
  const { setBreadcrumbs } = useBreadcrumbs();

  useEffect(() => {
    if (recipe) {
      setBreadcrumbs([
        { label: "Recipes", href: "/recipes" },
        { label: recipe.resultItem.name },
      ]);
    }
  }, [recipe, setBreadcrumbs]);

  const tabParam = searchParams.get("tab");
  const activeTab: TabKey = isValidTab(tabParam) ? tabParam : "shopping";

  function setActiveTab(tab: TabKey) {
    const params = new URLSearchParams(searchParams.toString());
    params.set("tab", tab);
    router.replace(`?${params.toString()}`, { scroll: false });
  }

  const recipeId = Number(params.id);
  const isValidId = !isNaN(recipeId);

  useEffect(() => {
    if (!isValidId) return;

    getRecipe(recipeId, true)
      .then((r) => {
        setRecipe(r);
        trackAnalyticsEvent("recipe_view", { recipeId, recipeName: r.resultItem.name });
      })
      .catch((err: unknown) => {
        setError(
          err instanceof Error ? err.message : "Failed to load recipe",
        );
      })
      .finally(() => setIsLoading(false));
  }, [isValidId, recipeId]);

  const {
    data: treeData,
    isLoading: isTreeLoading,
    error: treeError,
    retry: retryTree,
  } = useMaterialTree(recipeId, quantity);

  if (!isValidId) {
    return (
      <div className="p-4">
        <ErrorState message="Invalid recipe ID" />
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
  const fromGear = searchParams.get("from") === "gear";
  const equipInfo = recipe.equipmentInfo;

  return (
    <div className="p-4 space-y-6 max-w-2xl mx-auto">
      {/* Back navigation */}
      <button
        onClick={() => fromGear ? router.push("/gear") : router.back()}
        className="flex items-center gap-1 text-text-secondary hover:text-gold transition-colors text-sm"
      >
        <ArrowLeft size={16} />
        {fromGear ? "Back to Gear Browser" : "Back to search"}
      </button>

      {/* Enhanced header */}
      <div className="flex items-start gap-4">
        <ItemIcon
          iconUrl={recipe.resultItem.iconUrl}
          alt={recipe.resultItem.name}
          size="2xl"
        />
        <div className="flex-1 min-w-0">
          <h1 className="text-xl font-bold text-text-primary">
            {recipe.resultItem.name}
          </h1>
          <div className="flex items-center gap-2 mt-1 flex-wrap">
            <span className={`text-sm font-medium ${craftInfo.colorClass}`}>
              {craftInfo.fullName}
            </span>
            <span className="text-text-muted text-sm">
              Lv. {recipe.recipeLevel}
            </span>
            <span className="text-text-muted text-sm">
              iLv {recipe.resultItem.itemLevel}
            </span>
            {recipe.yield > 1 && (
              <span className="px-2 py-0.5 rounded-full text-xs font-medium bg-gold/20 text-gold">
                Yields {recipe.yield}
              </span>
            )}
          </div>
          {recipe.materialSummary && (
            <div className="flex items-center gap-2 mt-2 flex-wrap">
              <span className="text-xs text-text-muted bg-bg-elevated px-2 py-0.5 rounded">
                {recipe.materialSummary.rawMaterialCount} materials
              </span>
              <span className="text-xs text-text-muted bg-bg-elevated px-2 py-0.5 rounded">
                {recipe.materialSummary.crystalTypeCount} crystal types
              </span>
              {recipe.materialSummary.intermediateCraftCount > 0 && (
                <span className="text-xs text-text-muted bg-bg-elevated px-2 py-0.5 rounded">
                  {recipe.materialSummary.intermediateCraftCount} sub-crafts
                </span>
              )}
            </div>
          )}
        </div>
        <ProtectedAction message="Sign in to favorite recipes">
          <button
            onClick={async () => {
              setAnimatingFav(true);
              await toggleFavorite(recipeId);
              setTimeout(() => setAnimatingFav(false), 300);
            }}
            className={`p-1.5 rounded-md transition-all duration-200 ${
              isFavorited(recipeId)
                ? "text-red-400 hover:text-red-300"
                : "text-text-muted hover:text-red-400"
            } ${animatingFav ? "scale-125" : "scale-100"}`}
            aria-label={isFavorited(recipeId) ? "Remove from favorites" : "Add to favorites"}
          >
            <Heart size={20} fill={isFavorited(recipeId) ? "currentColor" : "none"} />
          </button>
        </ProtectedAction>
      </div>

      {/* Craftability banner */}
      {profile && <RecipeCraftabilityBanner recipe={recipe} profile={profile} />}

      {/* Recipe stats - 2 column on desktop */}
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
        <div className="bg-bg-surface rounded-lg p-4 space-y-3 border border-border">
          <h2 className="text-sm font-semibold text-text-secondary uppercase tracking-wider">
            Recipe Stats
          </h2>
          <StatBar label="Difficulty" value={recipe.difficulty} />
          <StatBar label="Quality" value={recipe.quality} />
          <StatBar label="Durability" value={recipe.durability} />
        </div>
        <div className="bg-bg-surface rounded-lg p-4 space-y-3 border border-border">
          <h2 className="text-sm font-semibold text-text-secondary uppercase tracking-wider">
            Required Stats
          </h2>
          <StatBar label="Craftsmanship" value={recipe.requiredCraftsmanship} />
          <StatBar label="Control" value={recipe.requiredControl} />
        </div>
      </div>

      {/* Journey button */}
      <Link
        href={`/recipes/${recipeId}/journey`}
        className="flex items-center gap-2 p-3 rounded-lg bg-gold/10 border border-gold/30 hover:bg-gold/20 transition-colors"
      >
        <Map className="text-gold shrink-0" size={18} />
        <span className="text-sm font-medium text-gold">Start Crafting Journey</span>
      </Link>

      {/* Simulate + Auto-Solve links */}
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
        <Link
          href={`/simulator?recipeId=${recipeId}`}
          className="flex items-center gap-2 p-3 rounded-lg bg-bg-surface border border-border hover:border-gold transition-colors"
        >
          <FlaskConical className="text-gold shrink-0" size={18} />
          <span className="text-sm text-text-primary">Simulate this Recipe</span>
        </Link>
        <Link
          href={`/simulator?recipeId=${recipeId}&solve=true`}
          className="flex items-center gap-2 p-3 rounded-lg bg-bg-surface border border-border hover:border-gold transition-colors"
        >
          <Sparkles className="text-gold shrink-0" size={18} />
          <span className="text-sm text-text-primary">Auto-Solve this Recipe</span>
        </Link>
      </div>

      {/* Equipment info banner */}
      {equipInfo && (
        <div className="rounded-lg border border-gold/30 bg-gold/5 p-4 space-y-3">
          <div className="flex items-center gap-2">
            <Shield size={18} className="text-gold" />
            <span className="text-sm font-medium text-gold">
              This is gear for {equipInfo.classJobCategory}
            </span>
          </div>
          <div className="grid grid-cols-3 gap-2">
            {equipInfo.craftsmanship > 0 && (
              <div className="bg-bg-elevated rounded-md p-2 text-center">
                <p className="text-sm font-bold text-text-primary">{equipInfo.craftsmanship}</p>
                <p className="text-xs text-text-muted">Craftsmanship</p>
              </div>
            )}
            {equipInfo.control > 0 && (
              <div className="bg-bg-elevated rounded-md p-2 text-center">
                <p className="text-sm font-bold text-text-primary">{equipInfo.control}</p>
                <p className="text-xs text-text-muted">Control</p>
              </div>
            )}
            {equipInfo.cp > 0 && (
              <div className="bg-bg-elevated rounded-md p-2 text-center">
                <p className="text-sm font-bold text-text-primary">{equipInfo.cp}</p>
                <p className="text-xs text-text-muted">CP</p>
              </div>
            )}
            {equipInfo.gathering > 0 && (
              <div className="bg-bg-elevated rounded-md p-2 text-center">
                <p className="text-sm font-bold text-text-primary">{equipInfo.gathering}</p>
                <p className="text-xs text-text-muted">Gathering</p>
              </div>
            )}
            {equipInfo.perception > 0 && (
              <div className="bg-bg-elevated rounded-md p-2 text-center">
                <p className="text-sm font-bold text-text-primary">{equipInfo.perception}</p>
                <p className="text-xs text-text-muted">Perception</p>
              </div>
            )}
            {equipInfo.gp > 0 && (
              <div className="bg-bg-elevated rounded-md p-2 text-center">
                <p className="text-sm font-bold text-text-primary">{equipInfo.gp}</p>
                <p className="text-xs text-text-muted">GP</p>
              </div>
            )}
          </div>
          <Link
            href={`/gear?job=${encodeURIComponent(equipInfo.classJobCategory.split(",")[0]?.trim() ?? "")}&level=${recipe.resultItem.itemLevel}&mode=recommend`}
            className="inline-flex items-center gap-1.5 text-sm text-gold hover:text-gold-light transition-colors"
          >
            <Shield size={14} />
            Compare with recommendations
          </Link>
        </div>
      )}

      {/* Direct ingredients */}
      <div className="bg-bg-surface rounded-lg p-4 border border-border">
        <h2 className="text-sm font-semibold text-text-secondary uppercase tracking-wider mb-3">
          Ingredients ({recipe.ingredients.length})
        </h2>
        {recipe.ingredients.length === 0 ? (
          <p className="text-sm text-text-muted py-2">
            This recipe has no ingredients.
          </p>
        ) : (
          <div className="space-y-2">
            {recipe.ingredients.map((ingredient) => (
              <div
                key={ingredient.itemId}
                className="flex items-center gap-3 p-2 rounded-lg hover:bg-bg-elevated transition-colors"
              >
                <ItemLink
                  itemId={ingredient.itemId}
                  itemName={ingredient.itemName}
                  iconUrl={ingredient.iconUrl}
                  showIcon={true}
                  iconSize="md"
                  compact
                  className="flex-1 min-w-0"
                />
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
              </div>
            ))}
          </div>
        )}
      </div>

      {/* Quantity multiplier */}
      <QuantityMultiplier value={quantity} onChange={setQuantity} />

      {/* Complex recipe banner */}
      {treeData && treeData.summary.maxTreeDepth > 5 && (
        <div className="flex items-center gap-2 px-4 py-2.5 rounded-lg bg-blue-500/10 border border-blue-500/20">
          <Info size={16} className="text-blue-400 shrink-0" />
          <span className="text-sm text-blue-300">
            This is a complex recipe with {treeData.summary.maxTreeDepth} levels
            of sub-crafts.
          </span>
        </div>
      )}

      {/* Tab navigation */}
      <div className="border-b border-border">
        <div className="flex gap-0">
          {TABS.map((tab) => {
            const Icon = tab.icon;
            const isActive = activeTab === tab.key;
            return (
              <button
                key={tab.key}
                onClick={() => setActiveTab(tab.key)}
                className={`flex items-center gap-1.5 px-4 py-3 text-sm font-medium border-b-2 transition-colors ${
                  isActive
                    ? "border-gold text-gold"
                    : "border-transparent text-text-muted hover:text-text-secondary"
                }`}
              >
                <Icon size={16} />
                {tab.label}
              </button>
            );
          })}
        </div>
      </div>

      {/* Tab content */}
      <div className="pb-4 min-h-[200px]">
        {isTreeLoading ? (
          <>
            {activeTab === "shopping" && <ShoppingListSkeleton />}
            {activeTab === "tree" && (
              <div className="flex items-center justify-center p-8 gap-3">
                <LoadingSpinner size={24} />
                <span className="text-sm text-text-muted">
                  Loading tree...
                </span>
              </div>
            )}
            {activeTab === "steps" && <CraftingStepsSkeleton />}
          </>
        ) : treeError ? (
          <ErrorState message={treeError} onRetry={retryTree} />
        ) : treeData ? (
          <>
            {activeTab === "shopping" && (
              <ShoppingListView
                shoppingList={treeData.shoppingList}
                crystalRequirements={treeData.crystalRequirements}
                intermediateCrafts={treeData.intermediateCrafts}
                itemName={recipe.resultItem.name}
                quantity={quantity}
                summary={treeData.summary}
                recipeId={recipeId}
              />
            )}
            {activeTab === "tree" && (
              <>
                {equipInfo && (
                  <div className="mb-3 px-3 py-2 rounded-md bg-gold/5 border border-gold/20 text-xs text-text-secondary">
                    This gear provides:{" "}
                    {[
                      equipInfo.craftsmanship > 0 && `Craftsmanship +${equipInfo.craftsmanship}`,
                      equipInfo.control > 0 && `Control +${equipInfo.control}`,
                      equipInfo.cp > 0 && `CP +${equipInfo.cp}`,
                      equipInfo.gathering > 0 && `Gathering +${equipInfo.gathering}`,
                      equipInfo.perception > 0 && `Perception +${equipInfo.perception}`,
                      equipInfo.gp > 0 && `GP +${equipInfo.gp}`,
                    ].filter(Boolean).join(", ")}
                  </div>
                )}
                <MaterialTree tree={treeData.tree} />
              </>
            )}
            {activeTab === "steps" && (
              <CraftingStepsView
                intermediateCrafts={treeData.intermediateCrafts}
                tree={treeData.tree}
              />
            )}
          </>
        ) : null}
      </div>
    </div>
  );
}

export default function RecipeDetailClient() {
  return (
    <Suspense
      fallback={
        <div className="flex items-center justify-center p-12">
          <LoadingSpinner size={32} />
        </div>
      }
    >
      <RecipeDetailContent />
    </Suspense>
  );
}
