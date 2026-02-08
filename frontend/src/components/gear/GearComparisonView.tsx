"use client";

import { ArrowUp, ArrowDown, Minus } from "lucide-react";
import { ItemIcon } from "@/components/ui/ItemIcon";
import { SourceBadge } from "@/components/ui/SourceBadge";
import type { EquipmentDto } from "@/types/gear";
import { RARITY_COLOR_CLASS } from "@/lib/gear-constants";

interface GearComparisonViewProps {
  recommended: EquipmentDto;
  selected: EquipmentDto;
  isDoH: boolean;
  onClose: () => void;
}

interface StatRow {
  label: string;
  recValue: number;
  altValue: number;
}

function getComparisonStats(
  rec: EquipmentDto,
  alt: EquipmentDto,
  isDoH: boolean,
): StatRow[] {
  if (isDoH) {
    return [
      { label: "Craftsmanship", recValue: rec.stats.craftsmanship, altValue: alt.stats.craftsmanship },
      { label: "Control", recValue: rec.stats.control, altValue: alt.stats.control },
      { label: "CP", recValue: rec.stats.cp, altValue: alt.stats.cp },
    ];
  }
  return [
    { label: "Gathering", recValue: rec.stats.gathering, altValue: alt.stats.gathering },
    { label: "Perception", recValue: rec.stats.perception, altValue: alt.stats.perception },
    { label: "GP", recValue: rec.stats.gp, altValue: alt.stats.gp },
  ];
}

export function GearComparisonView({
  recommended,
  selected,
  isDoH,
}: GearComparisonViewProps) {
  const recRarity = RARITY_COLOR_CLASS[recommended.rarityName] ?? "text-text-primary";
  const altRarity = RARITY_COLOR_CLASS[selected.rarityName] ?? "text-text-primary";
  const stats = getComparisonStats(recommended, selected, isDoH);
  const scoreDiff = selected.weightedScore - recommended.weightedScore;

  return (
    <div className="rounded-lg border border-border bg-bg-surface p-4 space-y-4 animate-scale-in" aria-live="polite">
      {/* Column headers */}
      <div className="grid grid-cols-[1fr_auto_1fr] gap-3 items-start">
        {/* Recommended */}
        <div className="text-center space-y-2">
          <p className="text-xs uppercase tracking-wider text-gold font-semibold">
            Recommended
          </p>
          <div className="flex flex-col items-center gap-1">
            <ItemIcon iconUrl={recommended.iconUrl} alt={recommended.name} size="lg" />
            <p className={`text-sm font-medium ${recRarity} line-clamp-2`}>
              {recommended.name}
            </p>
            <span className="text-xs text-text-muted">iLv {recommended.itemLevel}</span>
          </div>
        </div>

        {/* VS divider */}
        <div className="flex items-center justify-center pt-6">
          <span className="text-xs font-bold text-text-muted">VS</span>
        </div>

        {/* Alternative */}
        <div className="text-center space-y-2">
          <p className="text-xs uppercase tracking-wider text-text-secondary font-semibold">
            Alternative
          </p>
          <div className="flex flex-col items-center gap-1">
            <ItemIcon iconUrl={selected.iconUrl} alt={selected.name} size="lg" />
            <p className={`text-sm font-medium ${altRarity} line-clamp-2`}>
              {selected.name}
            </p>
            <span className="text-xs text-text-muted">iLv {selected.itemLevel}</span>
          </div>
        </div>
      </div>

      {/* Stat comparison rows */}
      <div className="space-y-1">
        {stats.map(({ label, recValue, altValue }) => {
          const diff = altValue - recValue;
          return (
            <div
              key={label}
              className="grid grid-cols-[1fr_auto_1fr] gap-3 items-center py-1.5 px-2 rounded-md bg-bg-elevated"
            >
              <span className="text-sm text-text-primary text-right font-medium tabular-nums">
                {recValue.toLocaleString()}
              </span>
              <div className="flex items-center gap-1 min-w-[80px] justify-center">
                {diff > 0 ? (
                  <ArrowUp size={12} className="text-rarity-uncommon" />
                ) : diff < 0 ? (
                  <ArrowDown size={12} className="text-red-400" />
                ) : (
                  <Minus size={12} className="text-text-muted" />
                )}
                <span className="text-xs text-text-muted">{label}</span>
                {diff !== 0 && (
                  <span
                    className={`text-xs font-medium ${diff > 0 ? "text-rarity-uncommon" : "text-red-400"}`}
                  >
                    {diff > 0 ? "+" : ""}{diff}
                  </span>
                )}
              </div>
              <span className="text-sm text-text-primary font-medium tabular-nums">
                {altValue.toLocaleString()}
              </span>
            </div>
          );
        })}
      </div>

      {/* Weighted score comparison */}
      <div className="grid grid-cols-[1fr_auto_1fr] gap-3 items-center py-2 px-2 rounded-md border border-border">
        <span className="text-sm text-gold text-right font-semibold tabular-nums">
          {recommended.weightedScore.toLocaleString(undefined, { maximumFractionDigits: 1 })}
        </span>
        <div className="flex items-center gap-1 min-w-[80px] justify-center">
          {scoreDiff > 0 ? (
            <ArrowUp size={12} className="text-rarity-uncommon" />
          ) : scoreDiff < 0 ? (
            <ArrowDown size={12} className="text-red-400" />
          ) : (
            <Minus size={12} className="text-text-muted" />
          )}
          <span className="text-xs text-text-muted">Score</span>
          {scoreDiff !== 0 && (
            <span
              className={`text-xs font-medium ${scoreDiff > 0 ? "text-rarity-uncommon" : "text-red-400"}`}
            >
              {scoreDiff > 0 ? "+" : ""}{scoreDiff.toFixed(1)}
            </span>
          )}
        </div>
        <span className="text-sm text-gold font-semibold tabular-nums">
          {selected.weightedScore.toLocaleString(undefined, { maximumFractionDigits: 1 })}
        </span>
      </div>

      {/* Source comparison */}
      <div className="grid grid-cols-[1fr_auto_1fr] gap-3 items-center">
        <div className="flex justify-end">
          {recommended.source && (
            <SourceBadge sourceType={recommended.source.type} />
          )}
        </div>
        <span className="text-xs text-text-muted text-center">Source</span>
        <div>
          {selected.source && (
            <SourceBadge sourceType={selected.source.type} />
          )}
        </div>
      </div>
    </div>
  );
}
