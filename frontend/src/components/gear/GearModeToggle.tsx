"use client";

import { Shield, Search } from "lucide-react";

interface GearModeToggleProps {
  mode: "recommend" | "search";
  onModeChange: (mode: "recommend" | "search") => void;
}

export function GearModeToggle({ mode, onModeChange }: GearModeToggleProps) {
  return (
    <div className="flex rounded-lg border border-border overflow-hidden">
      <button
        onClick={() => onModeChange("recommend")}
        className={`flex items-center gap-2 px-4 py-2 text-sm font-medium transition-colors flex-1 justify-center min-h-[44px] ${
          mode === "recommend"
            ? "bg-gold/15 text-gold border-r border-gold/30"
            : "bg-bg-surface text-text-secondary hover:text-text-primary border-r border-border"
        }`}
        aria-pressed={mode === "recommend"}
      >
        <Shield size={16} />
        Browse by Job
      </button>
      <button
        onClick={() => onModeChange("search")}
        className={`flex items-center gap-2 px-4 py-2 text-sm font-medium transition-colors flex-1 justify-center min-h-[44px] ${
          mode === "search"
            ? "bg-gold/15 text-gold"
            : "bg-bg-surface text-text-secondary hover:text-text-primary"
        }`}
        aria-pressed={mode === "search"}
      >
        <Search size={16} />
        Search by Name
      </button>
    </div>
  );
}
