"use client";

import { useCallback } from "react";
import { useRouter } from "next/navigation";
import { BookOpen } from "lucide-react";
import Link from "next/link";
import { useRotationLibrary } from "@/hooks/useRotationLibrary";
import { RotationCard } from "@/components/solver/RotationCard";
import { LoadingSpinner } from "@/components/ui/LoadingSpinner";
import type { RotationListItem } from "@/types/solver";

export function ProfileRotationsList() {
  const router = useRouter();
  const library = useRotationLibrary({ source: "User", pageSize: 6 });

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

  if (library.isLoading && library.rotations.length === 0) {
    return (
      <div className="flex items-center justify-center py-8">
        <LoadingSpinner size={24} />
      </div>
    );
  }

  if (library.rotations.length === 0) {
    return (
      <div className="text-center py-6 text-text-muted text-sm">
        <p>No saved rotations yet. Use the solver to generate and save rotations.</p>
      </div>
    );
  }

  return (
    <div className="space-y-3">
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
        {library.rotations.slice(0, 6).map((rotation) => (
          <RotationCard
            key={rotation.id}
            rotation={rotation}
            onView={handleView}
            onLoad={handleLoad}
            compact
          />
        ))}
      </div>
      {library.totalCount > 6 && (
        <Link
          href="/simulator/library?source=User"
          className="flex items-center gap-1.5 text-sm text-gold hover:text-gold-light transition-colors"
        >
          <BookOpen size={14} />
          View All ({library.totalCount})
        </Link>
      )}
    </div>
  );
}
