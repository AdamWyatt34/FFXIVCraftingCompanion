"use client";

import { SearchX, Info } from "lucide-react";

interface GearEmptyStateProps {
  type: "no-results" | "search-empty" | "low-level";
  job?: string;
  level?: number;
}

export function GearEmptyState({ type, job, level }: GearEmptyStateProps) {
  if (type === "low-level") {
    return (
      <div className="rounded-lg border border-blue-500/30 bg-blue-500/5 p-4 flex items-start gap-3">
        <Info size={20} className="text-blue-400 shrink-0 mt-0.5" />
        <div>
          <p className="text-sm font-medium text-blue-400">
            Limited gear at low levels
          </p>
          <p className="text-xs text-text-secondary mt-1">
            At low levels, gear options are limited. Most players acquire gear
            from quests and early vendors. Try a higher level for more options.
          </p>
        </div>
      </div>
    );
  }

  if (type === "search-empty") {
    return (
      <div className="flex flex-col items-center justify-center py-12 text-center">
        <SearchX size={40} className="text-text-muted mb-3" />
        <p className="text-text-secondary text-sm">
          No equipment matches your search.
        </p>
        <p className="text-text-muted text-xs mt-1">
          Try a different name or remove filters.
        </p>
      </div>
    );
  }

  return (
    <div className="flex flex-col items-center justify-center py-12 text-center">
      <SearchX size={40} className="text-text-muted mb-3" />
      <p className="text-text-secondary text-sm">
        No gear found for {job ?? "this job"} at level {level ?? "this level"}.
      </p>
      <p className="text-text-muted text-xs mt-1">
        Try a higher level or different job.
      </p>
    </div>
  );
}
