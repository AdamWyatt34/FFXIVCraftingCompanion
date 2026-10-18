"use client";

import { useCallback, Suspense } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { BookOpen, ArrowLeft } from "lucide-react";
import Link from "next/link";
import { useRotationLibrary } from "@/hooks/useRotationLibrary";
import { RotationCard } from "@/components/solver/RotationCard";
import { RotationFilters } from "@/components/solver/RotationFilters";
import { LoadingSpinner } from "@/components/ui/LoadingSpinner";
import { LoadingSkeleton } from "@/components/ui/LoadingStates";
import type { RotationListItem, RotationSearchParams } from "@/types/solver";

function LibraryContent() {
  const router = useRouter();
  const searchParams = useSearchParams();

  // Build initial filters from URL params
  const initialFilters: Partial<RotationSearchParams> = {};
  const recipeIdParam = searchParams.get("recipeId");
  const craftTypeParam = searchParams.get("craftType");
  const levelParam = searchParams.get("level");
  const sourceParam = searchParams.get("source");
  const sortByParam = searchParams.get("sortBy");

  if (recipeIdParam) initialFilters.recipeId = Number(recipeIdParam);
  if (craftTypeParam) initialFilters.craftType = craftTypeParam;
  if (levelParam) {
    initialFilters.minLevel = Number(levelParam);
    initialFilters.maxLevel = Number(levelParam);
  }
  if (sourceParam) initialFilters.source = sourceParam;
  if (sortByParam) initialFilters.sortBy = sortByParam as RotationSearchParams["sortBy"];

  const library = useRotationLibrary(initialFilters);

  const handleView = useCallback(
    (id: number) => {
      router.push(`/simulator/library/${id}`);
    },
    [router],
  );

  const handleLoad = useCallback(
    (rotation: RotationListItem) => {
      const params = new URLSearchParams({
        recipeId: String(rotation.recipeId),
        craftsmanship: String(rotation.craftsmanship),
        control: String(rotation.control),
        cp: String(rotation.cp),
        level: String(rotation.crafterLevel),
        rotation: rotation.actionIds.join(","),
      });
      router.push(`/simulator?${params.toString()}`);
    },
    [router],
  );

  return (
    <div className="p-4">
      <div className="max-w-4xl mx-auto">
        {/* Header */}
        <div className="flex items-center gap-3 mb-6">
          <Link
            href="/simulator"
            className="text-text-muted hover:text-text-primary transition-colors"
          >
            <ArrowLeft size={20} />
          </Link>
          <BookOpen className="text-gold" size={24} />
          <h1 className="text-xl font-bold">Rotation Library</h1>
          {library.totalCount > 0 && (
            <span className="text-sm text-text-muted">
              {library.totalCount} rotation
              {library.totalCount !== 1 ? "s" : ""}
            </span>
          )}
        </div>

        {/* Filters */}
        <div className="mb-6">
          <RotationFilters
            filters={library.filters}
            onFiltersChange={library.setFilters}
          />
        </div>

        {/* Error state */}
        {library.error && (
          <div className="text-center py-12">
            <p className="text-red-400 mb-3">{library.error}</p>
            <button
              onClick={library.retry}
              className="text-sm text-gold hover:text-gold-light transition-colors"
            >
              Try Again
            </button>
          </div>
        )}

        {/* Loading state (initial) */}
        {library.isLoading && library.rotations.length === 0 && !library.error && (
          <div className="flex items-center justify-center py-16">
            <LoadingSpinner size={32} />
          </div>
        )}

        {/* Empty state */}
        {!library.isLoading &&
          !library.error &&
          library.rotations.length === 0 && (
            <div className="text-center py-16 space-y-3">
              <BookOpen size={48} className="text-text-muted mx-auto" />
              <h2 className="text-lg font-semibold text-text-primary">
                No rotations found
              </h2>
              <p className="text-sm text-text-muted max-w-md mx-auto">
                Try adjusting your filters, or use the solver to generate and
                save a rotation.
              </p>
              <Link
                href="/simulator"
                className="inline-flex items-center gap-1.5 px-4 py-2 bg-gold text-bg-primary font-bold rounded-lg hover:bg-gold-light transition-colors text-sm mt-2"
              >
                Go to Simulator
              </Link>
            </div>
          )}

        {/* Rotation grid */}
        {library.rotations.length > 0 && (
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            {library.rotations.map((rotation) => (
              <RotationCard
                key={rotation.id}
                rotation={rotation}
                onView={handleView}
                onLoad={handleLoad}
              />
            ))}
          </div>
        )}

        {/* Load more */}
        {library.hasMore && (
          <div className="flex justify-center mt-6">
            <button
              onClick={library.loadMore}
              disabled={library.isLoading}
              className="flex items-center gap-2 px-6 py-2.5 bg-bg-surface border border-border text-text-secondary font-medium rounded-lg hover:text-text-primary hover:border-gold/30 disabled:opacity-50 disabled:cursor-not-allowed transition-colors text-sm"
            >
              {library.isLoading ? (
                <>
                  <LoadingSpinner size={14} />
                  Loading...
                </>
              ) : (
                `Load More (${library.rotations.length} of ${library.totalCount})`
              )}
            </button>
          </div>
        )}
      </div>
    </div>
  );
}

export default function RotationLibraryPage() {
  return (
    <Suspense
      fallback={
        <div className="p-4">
          <div className="max-w-4xl mx-auto">
            <div className="flex items-center gap-3 mb-6">
              <BookOpen className="text-gold" size={24} />
              <h1 className="text-xl font-bold">Rotation Library</h1>
            </div>
            <LoadingSkeleton count={4} />
          </div>
        </div>
      }
    >
      <LibraryContent />
    </Suspense>
  );
}
