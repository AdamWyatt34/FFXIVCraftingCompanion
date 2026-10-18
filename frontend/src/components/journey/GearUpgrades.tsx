"use client";

import { useState } from "react";
import Link from "next/link";
import { ChevronDown, ChevronRight, ArrowUp, Map } from "lucide-react";
import type { GearUpgradeSuggestionDto } from "@/types/journey";
import { recipeJourneyPath } from "@/lib/routes";

interface GearUpgradesProps {
  upgrades: GearUpgradeSuggestionDto[];
}

export function GearUpgrades({ upgrades }: GearUpgradesProps) {
  const [isExpanded, setIsExpanded] = useState(false);

  if (upgrades.length === 0) return null;

  return (
    <div className="bg-bg-surface rounded-lg border border-border overflow-hidden">
      <button
        onClick={() => setIsExpanded(!isExpanded)}
        className="w-full flex items-center gap-2 px-4 py-3 bg-bg-elevated/50 border-b border-border hover:bg-bg-elevated transition-colors min-h-[44px]"
      >
        {isExpanded ? (
          <ChevronDown size={16} className="text-text-muted shrink-0" />
        ) : (
          <ChevronRight size={16} className="text-text-muted shrink-0" />
        )}
        <ArrowUp size={16} className="text-amber-400" />
        <h3 className="text-sm font-semibold text-text-secondary uppercase tracking-wider">
          Gear Upgrades
        </h3>
        <span className="text-xs text-text-muted font-normal">
          ({upgrades.length})
        </span>
      </button>

      {isExpanded && (
        <div className="divide-y divide-border/50">
          {upgrades.map((upgrade, i) => (
            <div key={i} className="px-4 py-3 space-y-1">
              <div className="flex items-center justify-between">
                <span className="text-sm font-medium text-text-primary">
                  {upgrade.slot}
                </span>
                {upgrade.source && (
                  <span className="text-xs text-text-muted">{upgrade.source}</span>
                )}
              </div>
              <div className="flex items-center gap-2 text-xs">
                {upgrade.currentItem && (
                  <>
                    <span className="text-text-muted truncate">{upgrade.currentItem}</span>
                    <span className="text-text-muted">&rarr;</span>
                  </>
                )}
                <span className="text-gold truncate">{upgrade.suggestedItem}</span>
              </div>
              <div className="flex items-center gap-3 text-xs">
                {upgrade.craftsmanshipGain > 0 && (
                  <span className="text-emerald-400">+{upgrade.craftsmanshipGain} Craft</span>
                )}
                {upgrade.controlGain > 0 && (
                  <span className="text-emerald-400">+{upgrade.controlGain} Control</span>
                )}
                {upgrade.cpGain > 0 && (
                  <span className="text-emerald-400">+{upgrade.cpGain} CP</span>
                )}
              </div>
              {upgrade.recipeId && (
                <Link
                  href={recipeJourneyPath(upgrade.recipeId, upgrade.suggestedItem)}
                  className="inline-flex items-center gap-1 text-xs text-gold hover:text-gold-light transition-colors"
                >
                  <Map size={12} />
                  Craft This
                </Link>
              )}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
