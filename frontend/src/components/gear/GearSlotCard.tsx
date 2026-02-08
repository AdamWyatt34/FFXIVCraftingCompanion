"use client";

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
} from "lucide-react";
import { ItemIcon } from "@/components/ui/ItemIcon";
import { SourceBadge } from "@/components/ui/SourceBadge";
import type { GearSlotDto } from "@/types/gear";
import { RARITY_COLOR_CLASS } from "@/lib/gear-constants";
import type { LucideIcon } from "lucide-react";

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
}

export function GearSlotCard({
  slot,
  isDoH,
  isExpanded,
  onToggleExpand,
  maxScore,
}: GearSlotCardProps) {
  const { recommendation, isEmpty, slotName, slotKey } = slot;

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

  const item = recommendation;
  const rarityClass =
    RARITY_COLOR_CLASS[item.rarityName] ?? "text-text-primary";
  const scoreOpacity =
    maxScore && maxScore > 0
      ? Math.max(0.4, item.weightedScore / maxScore)
      : 1;

  return (
    <div
      className={`rounded-lg border bg-bg-surface p-3 space-y-2 cursor-pointer transition-all
        ${isExpanded ? "border-gold ring-1 ring-gold/30" : "border-border hover:border-gold/50 hover:bg-bg-elevated/50"}`}
      onClick={onToggleExpand}
      role="button"
      tabIndex={0}
      aria-expanded={isExpanded}
      onKeyDown={(e) => {
        if (e.key === "Enter" || e.key === " ") {
          e.preventDefault();
          onToggleExpand();
        }
      }}
    >
      {/* Slot label */}
      <p className="text-xs uppercase tracking-wider text-text-muted">
        {slotName}
      </p>

      {/* Main item row */}
      <div className="flex items-center gap-3">
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
      </div>

      {/* Stats row */}
      <div className="flex flex-wrap gap-x-3 gap-y-1 text-xs">
        {isDoH ? (
          <>
            <StatBadge
              label="Craft"
              value={item.stats.craftsmanship}
              opacity={scoreOpacity}
            />
            <StatBadge
              label="Ctrl"
              value={item.stats.control}
              opacity={scoreOpacity}
            />
            <StatBadge
              label="CP"
              value={item.stats.cp}
              opacity={scoreOpacity}
            />
          </>
        ) : (
          <>
            <StatBadge
              label="Gath"
              value={item.stats.gathering}
              opacity={scoreOpacity}
            />
            <StatBadge
              label="Perc"
              value={item.stats.perception}
              opacity={scoreOpacity}
            />
            <StatBadge
              label="GP"
              value={item.stats.gp}
              opacity={scoreOpacity}
            />
          </>
        )}
      </div>

      {/* Source badge */}
      {item.source && (
        <div className="flex items-center gap-2">
          <SourceBadge sourceType={item.source.type} />
          <span className="text-xs text-text-muted truncate flex-1">
            {item.source.description}
          </span>
          {item.source.hasRecipe && item.source.recipeId && (
            <Link
              href={`/recipes/${item.source.recipeId}?from=gear`}
              onClick={(e) => e.stopPropagation()}
              className="inline-flex items-center gap-1 px-2 py-1 rounded-md bg-gold/15 text-gold text-xs font-medium hover:bg-gold/25 transition-colors shrink-0"
            >
              <Hammer size={12} />
              Craft
            </Link>
          )}
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
