"use client";

import { useState } from "react";
import Link from "next/link";
import {
  Sparkles,
  Diamond,
  Sword,
  Shield,
  HardHat,
  Shirt,
  Hand,
  RectangleHorizontal,
  Footprints,
  Gem,
  Link as LinkIcon,
  CircleDot,
  Hammer,
  Check,
  ArrowRight,
  Map,
  ChevronLeft,
  ChevronRight,
} from "lucide-react";
import { ItemIcon } from "@/components/ui/ItemIcon";
import { SourceBadge } from "@/components/ui/SourceBadge";
import type { GearSlotDto, EquipmentDto } from "@/types/gear";
import type { SlotComparisonDto } from "@/types/gear-comparison";
import { RARITY_COLOR_CLASS } from "@/lib/gear-constants";
import type { LucideIcon } from "lucide-react";
import { recipeJourneyPath, recipePath } from "@/lib/routes";

const SLOT_ICONS: Record<string, LucideIcon> = {
  MainHand: Sword,
  OffHand: Shield,
  Head: HardHat,
  Body: Shirt,
  Hands: Hand,
  Legs: RectangleHorizontal,
  Feet: Footprints,
  Earring: Gem,
  Necklace: LinkIcon,
  Bracelet: CircleDot,
  Ring: Gem,
};

interface GearSlotCardProps {
  slot: GearSlotDto;
  isDoH: boolean;
  isExpanded: boolean;
  onToggleExpand: () => void;
  maxScore?: number;
  comparison?: SlotComparisonDto;
}

export function GearSlotCard({
  slot,
  isDoH,
  isExpanded,
  onToggleExpand,
  maxScore,
  comparison,
}: GearSlotCardProps) {
  const { recommendation, alternatives, isEmpty, slotName, slotKey } = slot;
  const [selectedIdx, setSelectedIdx] = useState(0);

  if (isEmpty || !recommendation) {
    const SlotIcon = SLOT_ICONS[slotKey] ?? Gem;
    return (
      <div className="rounded-lg border-2 border-dashed border-border p-3 flex flex-col items-center justify-center min-h-[120px]">
        <SlotIcon size={24} className="text-text-muted mb-2" />
        <p className="text-xs uppercase tracking-wider text-text-muted">
          {slotName}
        </p>
        <p className="text-xs text-text-muted mt-1">
          No gear available at this level
        </p>
      </div>
    );
  }

  // Build list: recommendation first, then alternatives
  const allItems: EquipmentDto[] = [recommendation, ...(alternatives ?? [])].filter(Boolean);
  const hasAlternatives = allItems.length > 1;
  const safeIdx = Math.min(selectedIdx, allItems.length - 1);
  const item = allItems[safeIdx] ?? recommendation;
  const isShowingRecommendation = safeIdx === 0;

  const rarityClass =
    RARITY_COLOR_CLASS[item.rarityName] ?? "text-text-primary";
  const scoreOpacity =
    maxScore && maxScore > 0
      ? Math.max(0.4, item.weightedScore / maxScore)
      : 1;

  // Comparison data only applies to the recommendation (index 0)
  const compStatus = isShowingRecommendation ? comparison?.comparison?.status : undefined;
  const isUpgrade = compStatus === "Upgrade";
  const isEquipped = compStatus === "Equipped";
  const isAlternativeComp = compStatus === "Alternative";

  // Build stat diffs for upgrade badge (only for recommendation)
  const isCombat = item.stats.vitality > 0;
  const compStatDiffs = isShowingRecommendation
    ? (comparison?.comparison?.statDifferences?.filter((d) => {
        if (d.difference === 0) return false;
        if (isDoH) {
          return ["Craftsmanship", "Control", "CP"].includes(d.statName);
        }
        if (isCombat) {
          return ["PhysicalDamage", "Vitality", "CriticalHit", "Determination", "DirectHitRate", "Defense"].includes(d.statName);
        }
        return ["Gathering", "Perception", "GP"].includes(d.statName);
      }) ?? [])
    : [];

  const STAT_SHORT_NAMES: Record<string, string> = {
    Craftsmanship: "Craft",
    Control: "Ctrl",
    CP: "CP",
    Gathering: "Gath",
    Perception: "Perc",
    GP: "GP",
    PhysicalDamage: "DMG",
    Vitality: "VIT",
    CriticalHit: "Crit",
    Determination: "Det",
    DirectHitRate: "DH",
    Defense: "DEF",
    MagicDefense: "MDEF",
    Strength: "STR",
    Dexterity: "DEX",
    Intelligence: "INT",
    Mind: "MND",
  };

  const handlePrev = (e: React.MouseEvent) => {
    e.stopPropagation();
    setSelectedIdx((prev) => (prev <= 0 ? allItems.length - 1 : prev - 1));
  };

  const handleNext = (e: React.MouseEvent) => {
    e.stopPropagation();
    setSelectedIdx((prev) => (prev >= allItems.length - 1 ? 0 : prev + 1));
  };

  return (
    <div
      className={`rounded-lg border bg-bg-surface p-3 space-y-2 cursor-pointer transition-all relative
        ${isUpgrade ? "border-l-4 border-l-emerald-500 " : ""}${isExpanded ? "border-gold ring-1 ring-gold/30" : "border-border hover:border-gold/50 hover:bg-bg-elevated/50"}`}
      onClick={onToggleExpand}
      role="button"
      tabIndex={0}
      aria-expanded={isExpanded}
      onKeyDown={(e) => {
        if (e.key === "Enter" || e.key === " ") {
          e.preventDefault();
          onToggleExpand();
        }
        if (hasAlternatives && e.key === "ArrowLeft") {
          e.preventDefault();
          setSelectedIdx((prev) => (prev <= 0 ? allItems.length - 1 : prev - 1));
        }
        if (hasAlternatives && e.key === "ArrowRight") {
          e.preventDefault();
          setSelectedIdx((prev) => (prev >= allItems.length - 1 ? 0 : prev + 1));
        }
      }}
    >
      {/* Comparison badge */}
      {isUpgrade && (
        <span className="inline-flex items-center gap-1 text-xs font-medium text-emerald-400 bg-emerald-500/10 px-2 py-0.5 rounded-full">
          <TrendingUpMini /> Upgrade
        </span>
      )}
      {isEquipped && (
        <span className="inline-flex items-center gap-1 text-xs font-medium text-text-muted">
          <Check size={12} /> Equipped
        </span>
      )}
      {isAlternativeComp && (
        <span className="inline-flex items-center gap-1 text-xs font-medium text-amber-400 bg-amber-500/10 px-2 py-0.5 rounded-full">
          <ArrowRight size={12} /> Sidegrade
        </span>
      )}

      {/* Slot label + alternative indicator */}
      <div className="flex items-center justify-between">
        <p className="text-xs uppercase tracking-wider text-text-muted">
          {slotName}
        </p>
        {hasAlternatives && !isShowingRecommendation && (
          <span className="text-[10px] text-amber-400 font-medium">
            Alt {safeIdx}/{allItems.length - 1}
          </span>
        )}
      </div>

      {/* Main item row */}
      <div className="flex items-center gap-3">
        {/* Left arrow */}
        {hasAlternatives && (
          <button
            onClick={handlePrev}
            className="p-0.5 rounded text-text-muted hover:text-gold transition-colors shrink-0"
            aria-label="Previous alternative"
          >
            <ChevronLeft size={16} />
          </button>
        )}
        <ItemIcon iconUrl={item.iconUrl} alt={item.name} size="lg" />
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2">
            <p className={`font-medium truncate ${rarityClass}`}>
              {item.name}
            </p>
            {item.canBeHQ && (
              <span title={item.hqNote ?? "HQ version available with higher stats"}>
                <Sparkles size={14} className="text-gold shrink-0" />
              </span>
            )}
          </div>
          <div className="flex items-center gap-2 text-xs text-text-secondary">
            <span className="bg-bg-elevated px-1.5 py-0.5 rounded text-text-primary">
              iLv {item.itemLevel}
            </span>
            <span className="text-text-muted">Lv. {item.equipLevel}</span>
            {item.materiaSlots > 0 && (
              <span
                className="flex items-center gap-0.5"
                title={`${item.materiaSlots} materia slot${item.materiaSlots > 1 ? "s" : ""}`}
              >
                {Array.from({ length: item.materiaSlots }, (_, i) => (
                  <Diamond
                    key={i}
                    size={8}
                    className="text-blue-400 fill-blue-400"
                  />
                ))}
              </span>
            )}
          </div>
        </div>
        {/* Right arrow */}
        {hasAlternatives && (
          <button
            onClick={handleNext}
            className="p-0.5 rounded text-text-muted hover:text-gold transition-colors shrink-0"
            aria-label="Next alternative"
          >
            <ChevronRight size={16} />
          </button>
        )}
      </div>

      {/* Stats row */}
      <div className="flex flex-wrap gap-x-3 gap-y-1 text-xs">
        {isDoH ? (
          <>
            <StatBadge label="Craft" value={item.stats.craftsmanship} opacity={scoreOpacity} />
            <StatBadge label="Ctrl" value={item.stats.control} opacity={scoreOpacity} />
            <StatBadge label="CP" value={item.stats.cp} opacity={scoreOpacity} />
          </>
        ) : item.stats.vitality > 0 ? (
          <>
            <StatBadge label="DMG" value={item.stats.physicalDamage} opacity={scoreOpacity} />
            <StatBadge label="VIT" value={item.stats.vitality} opacity={scoreOpacity} />
            <StatBadge label="Crit" value={item.stats.criticalHit} opacity={scoreOpacity} />
            <StatBadge label="Det" value={item.stats.determination} opacity={scoreOpacity} />
            <StatBadge label="DH" value={item.stats.directHitRate} opacity={scoreOpacity} />
            <StatBadge label="DEF" value={item.stats.defense} opacity={scoreOpacity} />
          </>
        ) : (
          <>
            <StatBadge label="Gath" value={item.stats.gathering} opacity={scoreOpacity} />
            <StatBadge label="Perc" value={item.stats.perception} opacity={scoreOpacity} />
            <StatBadge label="GP" value={item.stats.gp} opacity={scoreOpacity} />
          </>
        )}
      </div>

      {/* Upgrade stat diffs */}
      {isUpgrade && compStatDiffs.length > 0 && (
        <div className="flex flex-wrap gap-x-2 gap-y-0.5 text-xs">
          {compStatDiffs.map((d) => (
            <span key={d.statName} className="font-medium text-emerald-400">
              +{d.difference} {STAT_SHORT_NAMES[d.statName] ?? d.statName}
            </span>
          ))}
        </div>
      )}

      {/* Source badge */}
      {item.source && (
        <div className="flex items-center gap-2">
          <SourceBadge sourceType={item.source.type} />
          <span className="text-xs text-text-muted truncate flex-1">
            {item.source.description}
          </span>
          {item.source.hasRecipe && item.source.recipeId && (
            <div className="flex items-center gap-1 shrink-0">
              <Link
                href={`${recipePath(item.source.recipeId, item.name)}?from=gear`}
                onClick={(e) => e.stopPropagation()}
                className="inline-flex items-center gap-1 px-2 py-1 rounded-md bg-gold/15 text-gold text-xs font-medium hover:bg-gold/25 transition-colors"
              >
                <Hammer size={12} />
                Craft
              </Link>
              <Link
                href={recipeJourneyPath(item.source.recipeId, item.name)}
                onClick={(e) => e.stopPropagation()}
                className="inline-flex items-center gap-1 px-2 py-1 rounded-md bg-bg-elevated text-text-secondary text-xs font-medium hover:text-gold transition-colors"
              >
                <Map size={12} />
              </Link>
            </div>
          )}
        </div>
      )}

      {/* Dot indicators for alternatives */}
      {hasAlternatives && (
        <div className="flex items-center justify-center gap-1 pt-1">
          {allItems.map((_, i) => (
            <button
              key={i}
              onClick={(e) => {
                e.stopPropagation();
                setSelectedIdx(i);
              }}
              className={`w-1.5 h-1.5 rounded-full transition-colors ${
                i === safeIdx ? "bg-gold" : "bg-border hover:bg-text-muted"
              }`}
              aria-label={`View item ${i + 1} of ${allItems.length}`}
            />
          ))}
        </div>
      )}
    </div>
  );
}

function StatBadge({
  label,
  value,
  opacity,
}: {
  label: string;
  value: number;
  opacity?: number;
}) {
  if (value === 0) return null;
  return (
    <span
      className="text-text-secondary"
      style={opacity !== undefined ? { opacity } : undefined}
    >
      <span className="text-text-muted">{label}</span>{" "}
      <span className="text-text-primary font-medium">{value}</span>
    </span>
  );
}

// Inline mini arrow icon for upgrade badge (avoids importing TrendingUp for a tiny indicator)
function TrendingUpMini() {
  return (
    <svg
      width="12"
      height="12"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
    >
      <polyline points="22 7 13.5 15.5 8.5 10.5 2 17" />
      <polyline points="16 7 22 7 22 13" />
    </svg>
  );
}
