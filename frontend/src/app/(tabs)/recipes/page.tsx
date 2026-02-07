"use client";

import { Suspense } from "react";
import { useSearchParams } from "next/navigation";
import { ChefHat } from "lucide-react";
import {
  SearchBar,
  ItemCard,
  LoadingSkeleton,
  ErrorState,
  EmptyState,
  LoadingSpinner,
} from "@/components/ui";
import { useItemSearch } from "@/hooks/useItemSearch";
import { useInfiniteScroll } from "@/hooks/useInfiniteScroll";
import { MIN_SEARCH_LENGTH, CRAFT_TYPE_INFO } from "@/lib/constants";
import type { CraftType } from "@/types/api";

function RecipesPageContent() {
  const searchParams = useSearchParams();
  const initialQuery = searchParams.get("q") ?? "";

  const {
    query,
    setQuery,
    items,
    totalCount,
    isLoading,
    isLoadingMore,
    error,
    hasMore,
    loadMore,
    retry,
  } = useItemSearch(initialQuery);

  const sentinelRef = useInfiniteScroll({
    onLoadMore: loadMore,
    hasMore,
    isLoading: isLoadingMore,
  });

  const hasQuery = query.length >= MIN_SEARCH_LENGTH;
  const showWelcome = !hasQuery && !isLoading;
  const showResults = hasQuery && items.length > 0;
  const showEmpty = hasQuery && !isLoading && !error && items.length === 0;
  const showError = hasQuery && !isLoading && error !== null;
  const showSkeleton = hasQuery && isLoading && items.length === 0;

  return (
    <div className="flex flex-col h-full">
      {/* Sticky search bar */}
      <div className="sticky top-14 z-30 bg-bg-primary p-4 border-b border-border">
        <SearchBar
          value={query}
          onChange={setQuery}
          isLoading={isLoading}
          autoFocus
        />
        {showResults && (
          <p className="text-xs text-text-muted mt-2">
            {totalCount.toLocaleString()} result
            {totalCount !== 1 ? "s" : ""} found
          </p>
        )}
      </div>

      {/* Welcome state */}
      {showWelcome && (
        <div className="flex flex-col items-center gap-6 p-8 text-center">
          <ChefHat className="text-gold" size={48} />
          <div>
            <h1 className="text-xl font-bold text-text-primary">
              Recipe Search
            </h1>
            <p className="text-text-secondary text-sm mt-2">
              Search for any FFXIV item, recipe, or material
            </p>
          </div>
          <div className="flex flex-wrap justify-center gap-2">
            {(
              Object.entries(CRAFT_TYPE_INFO) as [
                CraftType,
                (typeof CRAFT_TYPE_INFO)[CraftType],
              ][]
            ).map(([key, info]) => (
              <button
                key={key}
                onClick={() => setQuery(info.fullName)}
                className={`px-3 py-1.5 text-sm rounded-full bg-bg-surface border border-border hover:border-border-focus transition-colors ${info.colorClass}`}
              >
                {info.fullName}
              </button>
            ))}
          </div>
          <div className="flex flex-wrap justify-center gap-2 mt-2">
            {["Dawntrail Recipes", "Level 100 Gear", "Common Materials"].map(
              (suggestion) => (
                <button
                  key={suggestion}
                  onClick={() => setQuery(suggestion)}
                  className="px-3 py-1.5 text-sm rounded-full bg-bg-elevated text-text-secondary border border-border hover:border-border-focus hover:text-text-primary transition-colors"
                >
                  {suggestion}
                </button>
              ),
            )}
          </div>
        </div>
      )}

      {/* Loading skeleton */}
      {showSkeleton && <LoadingSkeleton />}

      {/* Error state */}
      {showError && <ErrorState message={error!} onRetry={retry} />}

      {/* Empty state */}
      {showEmpty && <EmptyState query={query} />}

      {/* Results grid */}
      {showResults && (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3 p-4">
          {items.map((item) => (
            <ItemCard key={item.itemId} item={item} />
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
