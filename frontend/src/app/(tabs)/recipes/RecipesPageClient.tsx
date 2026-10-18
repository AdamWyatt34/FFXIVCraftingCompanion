"use client";

import { Suspense, useEffect } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { ChefHat, X, ClipboardList } from "lucide-react";
import { useBreadcrumbs } from "@/contexts/BreadcrumbContext";
import {
  SearchBar,
  ItemCard,
  LoadingSkeleton,
  ErrorState,
  EmptyState,
  LoadingSpinner,
} from "@/components/ui";
import Link from "next/link";
import { useItemSearch } from "@/hooks/useItemSearch";
import { useInfiniteScroll } from "@/hooks/useInfiniteScroll";
import { useCraftPlanContext } from "@/contexts/CraftPlanContext";
import { useFavorites } from "@/hooks/useFavorites";
import { MIN_SEARCH_LENGTH, CRAFT_TYPE_INFO } from "@/lib/constants";
import { QUICK_FILTERS, isQuickFilter } from "@/lib/static-data/search";
import type { CraftType } from "@/types/api";

function RecipesPageContent() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const initialQuery = searchParams.get("q") ?? "";
  // ?craftType= and ?quickFilter= come from the browse links; they are client-side filters and
  // the page stays canonical to /recipes.
  const craftTypeParam = searchParams.get("craftType");
  const urlCraftType = craftTypeParam && craftTypeParam in CRAFT_TYPE_INFO ? craftTypeParam : null;
  const quickFilterParam = searchParams.get("quickFilter");
  const urlQuickFilter = isQuickFilter(quickFilterParam) ? quickFilterParam : null;

  const { setBreadcrumbs } = useBreadcrumbs();
  useEffect(() => {
    setBreadcrumbs([{ label: "Recipes" }]);
  }, [setBreadcrumbs]);

  const { isFavorited } = useFavorites();
  const { planItemCount } = useCraftPlanContext();

  const {
    query,
    setQuery,
    craftType,
    setCraftType,
    quickFilter,
    setQuickFilter,
    items,
    totalCount,
    isLoading,
    isLoadingMore,
    error,
    hasMore,
    loadMore,
    retry,
  } = useItemSearch(initialQuery, urlCraftType, urlQuickFilter);

  // A later navigation to a filter link (the chips below the results) applies that filter.
  useEffect(() => {
    if (urlQuickFilter) setQuickFilter(urlQuickFilter);
    else if (urlCraftType) setCraftType(urlCraftType);
  }, [urlQuickFilter, urlCraftType, setQuickFilter, setCraftType]);

  const sentinelRef = useInfiniteScroll({
    onLoadMore: loadMore,
    hasMore,
    isLoading: isLoadingMore,
  });

  const hasQuery = query.length >= MIN_SEARCH_LENGTH;
  const hasFilter = craftType !== null || quickFilter !== null;
  const isActive = hasQuery || hasFilter;
  const showWelcome = !isActive && !isLoading;
  const showResults = isActive && items.length > 0;
  const showEmpty = isActive && !isLoading && !error && items.length === 0;
  const showError = isActive && !isLoading && error !== null;
  const showSkeleton = isActive && isLoading && items.length === 0;

  const activeCraftInfo = craftType
    ? CRAFT_TYPE_INFO[craftType as CraftType]
    : null;
  const activeFilterLabel = activeCraftInfo?.fullName ?? (quickFilter ? QUICK_FILTERS[quickFilter].label : null);

  function clearFilter() {
    if (activeCraftInfo) setCraftType(null);
    else setQuickFilter(null);
    if (craftTypeParam || quickFilterParam) router.replace("/recipes", { scroll: false });
  }

  return (
    <div className="flex flex-col">
      {/* Sticky search bar */}
      <div className="sticky top-14 z-30 bg-bg-primary p-4 border-b border-border">
        <div className="flex items-center gap-2">
          <div className="flex-1">
            <SearchBar
              value={query}
              onChange={setQuery}
              isLoading={isLoading}
            />
          </div>
          <Link
            href="/recipes/plan"
            className="flex items-center gap-1.5 px-3 py-2 text-sm text-text-secondary hover:text-gold transition-colors whitespace-nowrap"
          >
            <ClipboardList size={16} />
            Craft Plan
            {planItemCount > 0 && (
              <span className="ml-0.5 px-1.5 py-0.5 text-xs font-bold bg-gold/20 text-gold rounded-full">
                {planItemCount}
              </span>
            )}
          </Link>
        </div>
        {/* Active filter chip (craft type or quick filter) */}
        {activeFilterLabel && (
          <div className="flex items-center gap-2 mt-2">
            <span className="text-xs text-text-muted">Filtered by:</span>
            <button
              onClick={clearFilter}
              className={`inline-flex items-center gap-1 px-2.5 py-1 text-sm rounded-full bg-bg-surface border border-border-focus ${activeCraftInfo?.colorClass ?? "text-text-primary"}`}
            >
              {activeFilterLabel}
              <X size={14} />
            </button>
          </div>
        )}
        {showResults && (
          <p className="text-xs text-text-muted mt-2">
            {totalCount.toLocaleString()} result
            {totalCount !== 1 ? "s" : ""} found
          </p>
        )}
      </div>

      {/* Welcome state: the job hubs and quick filters are rendered by the page below the search */}
      {showWelcome && (
        <div className="flex flex-col items-center gap-3 p-8 text-center">
          <ChefHat className="text-gold" size={40} />
          <p className="text-text-secondary text-sm">
            Type an item name to search, or browse by job and level below
          </p>
        </div>
      )}

      {/* Loading skeleton */}
      {showSkeleton && <LoadingSkeleton />}

      {/* Error state */}
      {showError && <ErrorState message={error!} onRetry={retry} />}

      {/* Empty state */}
      {showEmpty && <EmptyState query={activeFilterLabel ?? query} />}

      {/* Results grid */}
      {showResults && (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3 p-4">
          {items.map((item) => (
            <ItemCard
              key={item.itemId}
              item={item}
              isFavorited={item.recipeId ? isFavorited(item.recipeId) : false}
            />
          ))}
        </div>
      )}

      {/* Infinite scroll sentinel */}
      {hasMore && (
        <div ref={sentinelRef} className="flex justify-center py-4">
          {isLoadingMore && <LoadingSpinner size={24} />}
        </div>
      )}
    </div>
  );
}

export default function RecipesPage() {
  return (
    <Suspense fallback={<LoadingSkeleton />}>
      <RecipesPageContent />
    </Suspense>
  );
}
