"use client";

import type { StatWeightProfileDto } from "@/types/gear";
import { PROFILE_DISPLAY_NAMES } from "@/lib/gear-constants";

interface ProfileSelectorProps {
  profiles: StatWeightProfileDto[];
  selectedProfile: string;
  onProfileSelect: (profileName: string) => void;
  isLoading: boolean;
}

export function ProfileSelector({
  profiles,
  selectedProfile,
  onProfileSelect,
  isLoading,
}: ProfileSelectorProps) {
  if (isLoading) {
    return (
      <div className="space-y-2">
        <p className="text-sm font-medium text-text-secondary">Stat Priority</p>
        <div className="flex flex-wrap gap-2">
          {[1, 2, 3].map((i) => (
            <div key={i} className="h-8 w-20 rounded-full bg-bg-elevated animate-pulse" />
          ))}
        </div>
      </div>
    );
  }

  if (profiles.length === 0) return null;

  return (
    <div className="space-y-2">
      <p className="text-sm font-medium text-text-secondary">Stat Priority</p>
      <div className="flex flex-wrap gap-2">
        {profiles.map((p) => {
          const isSelected = p.name === selectedProfile;
          const label = PROFILE_DISPLAY_NAMES[p.name] ?? p.displayName;
          return (
            <button
              key={p.name}
              onClick={() => onProfileSelect(p.name)}
              title={p.description}
              className={`px-4 py-1.5 min-h-[44px] rounded-full text-sm font-medium transition-colors ${
                isSelected
                  ? "bg-gold text-bg-primary"
                  : "bg-bg-surface border border-border text-text-secondary hover:border-border-focus"
              }`}
            >
              {label}
            </button>
          );
        })}
      </div>
    </div>
  );
}
