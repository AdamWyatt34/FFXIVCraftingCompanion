"use client";

import { useState, useEffect } from "react";
import { createPortal } from "react-dom";
import Link from "next/link";
import {
  X,
  Sparkles,
  Diamond,
  Hammer,
  ArrowLeftRight,
  ArrowLeft,
  Map,
  ClipboardList,
} from "lucide-react";
import { ItemIcon } from "@/components/ui/ItemIcon";
import { SourceBadge } from "@/components/ui/SourceBadge";
import type { GearSlotDto, EquipmentDto } from "@/types/gear";
import type { SlotComparisonDto } from "@/types/gear-comparison";
import { GearComparisonView } from "./GearComparisonView";
import { RARITY_COLOR_CLASS } from "@/lib/gear-constants";
import { recipeJourneyPath, recipePath } from "@/lib/routes";

interface GearSlotExpandedProps {
  slot: GearSlotDto;
  isDoH: boolean;
  onClose: () => void;
  comparison?: SlotComparisonDto;
  onApplySingle?: (slotKey: string, equipmentId: number) => void;
}

// Stat diff helper (exported for GearComparisonView)
export function getStatDiffs(
  rec: EquipmentDto,
  alt: EquipmentDto,
  isDoH: boolean,
): { label: string; diff: number }[] {
  const isCombat = rec.stats.vitality > 0 || alt.stats.vitality > 0;
  let diffs: { label: string; diff: number }[];

  if (isDoH) {
    diffs = [
      { label: "Craft", diff: alt.stats.craftsmanship - rec.stats.craftsmanship },
      { label: "Ctrl", diff: alt.stats.control - rec.stats.control },
      { label: "CP", diff: alt.stats.cp - rec.stats.cp },
    ];
  } else if (isCombat) {
    diffs = [
      { label: "DMG", diff: alt.stats.physicalDamage - rec.stats.physicalDamage },
      { label: "VIT", diff: alt.stats.vitality - rec.stats.vitality },
      { label: "Crit", diff: alt.stats.criticalHit - rec.stats.criticalHit },
      { label: "Det", diff: alt.stats.determination - rec.stats.determination },
      { label: "DH", diff: alt.stats.directHitRate - rec.stats.directHitRate },
      { label: "DEF", diff: alt.stats.defense - rec.stats.defense },
    ];
  } else {
    diffs = [
      { label: "Gath", diff: alt.stats.gathering - rec.stats.gathering },
      { label: "Perc", diff: alt.stats.perception - rec.stats.perception },
      { label: "GP", diff: alt.stats.gp - rec.stats.gp },
    ];
  }
  return diffs.filter((d) => d.diff !== 0);
}

// Shared content for both inline and bottom sheet
export function GearSlotExpandedContent({
  slot,
  isDoH,
  onClose,
  comparison,
  onApplySingle,
}: GearSlotExpandedProps) {
  const { recommendation, alternatives, slotName } = slot;
  const [comparisonItem, setComparisonItem] = useState<EquipmentDto | null>(null);

  if (!recommendation) return null;

  const item = recommendation;
  const rarityClass =
    RARITY_COLOR_CLASS[item.rarityName] ?? "text-text-primary";

  const isCombatItem = item.stats.vitality > 0;
  let statEntries: { label: string; value: number }[];

  if (isDoH) {
    statEntries = [
      { label: "Craftsmanship", value: item.stats.craftsmanship },
      { label: "Control", value: item.stats.control },
      { label: "CP", value: item.stats.cp },
    ];
  } else if (isCombatItem) {
    statEntries = [
      { label: "Weapon DMG", value: item.stats.physicalDamage },
      { label: "Vitality", value: item.stats.vitality },
      { label: "Defense", value: item.stats.defense },
      { label: "Crit", value: item.stats.criticalHit },
      { label: "Det", value: item.stats.determination },
      { label: "Direct Hit", value: item.stats.directHitRate },
    ].filter(e => e.value > 0);
  } else {
    statEntries = [
      { label: "Gathering", value: item.stats.gathering },
      { label: "Perception", value: item.stats.perception },
      { label: "GP", value: item.stats.gp },
    ];
  }

  return (
    <div className="space-y-4">
      {/* Header with close button */}
      <div className="flex items-start justify-between">
        <div className="flex items-center gap-3">
          <ItemIcon iconUrl={item.iconUrl} alt={item.name} size="xl" />
          <div>
            <p className={`text-lg font-bold ${rarityClass}`}>{item.name}</p>
            <div className="flex items-center gap-2 text-sm text-text-secondary mt-0.5">
              <span className="bg-bg-elevated px-2 py-0.5 rounded text-text-primary text-xs">
                iLv {item.itemLevel}
              </span>
              <span>Lv. {item.equipLevel}</span>
              <span className="text-text-muted">{slotName}</span>
            </div>
            <div className="flex items-center gap-2 mt-1">
              {item.canBeHQ && (
                <span
                  className="inline-flex items-center gap-1 text-xs text-gold"
                  title={item.hqNote ?? "HQ version available"}
                >
                  <Sparkles size={12} />
                  HQ Available
                </span>
              )}
              {item.materiaSlots > 0 && (
                <span className="inline-flex items-center gap-0.5 text-xs text-blue-400">
                  {Array.from({ length: item.materiaSlots }, (_, i) => (
                    <Diamond key={i} size={8} className="fill-blue-400" />
                  ))}
                  <span className="ml-1">
                    {item.materiaSlots} slot
                    {item.materiaSlots > 1 ? "s" : ""}
                  </span>
                </span>
              )}
            </div>
          </div>
        </div>
        <button
          onClick={onClose}
          className="p-1 text-text-muted hover:text-text-primary transition-colors rounded"
          aria-label="Close details"
        >
          <X size={20} />
        </button>
      </div>

      {/* Full stats */}
      <div className="grid grid-cols-3 gap-3">
        {statEntries.map(({ label, value }) => (
          <div
            key={label}
            className="bg-bg-elevated rounded-lg p-2.5 text-center"
          >
            <p className="text-base font-bold text-text-primary">
              {value.toLocaleString()}
            </p>
            <p className="text-xs text-text-muted">{label}</p>
          </div>
        ))}
      </div>

      {/* Score */}
      <div className="flex justify-between items-center text-sm px-1">
        <span className="text-text-muted">Weighted Score</span>
        <span className="text-gold font-semibold">
          {item.weightedScore.toLocaleString(undefined, {
            maximumFractionDigits: 1,
          })}
        </span>
      </div>

      {/* Source details */}
      {item.source && (
        <div className="bg-bg-elevated rounded-lg p-3 space-y-2">
          <div className="flex items-center gap-2">
            <SourceBadge sourceType={item.source.type} />
            <span className="text-sm text-text-secondary">
              {item.source.description}
            </span>
          </div>
          {item.source.hasRecipe && item.source.recipeId && (
            <div className="space-y-2">
              <Link
                href={`${recipePath(item.source.recipeId, item.name)}?from=gear`}
                className="flex items-center justify-center gap-2 w-full py-2.5 rounded-lg bg-gold/15 text-gold font-medium hover:bg-gold/25 transition-colors text-sm"
                onClick={(e) => e.stopPropagation()}
              >
                <Hammer size={16} />
                View Recipe & Materials
              </Link>
              <div className="flex gap-2">
                <Link
                  href={recipeJourneyPath(item.source.recipeId, item.name)}
                  className="flex items-center justify-center gap-2 flex-1 py-2 rounded-lg bg-bg-elevated text-text-secondary font-medium hover:text-gold transition-colors text-sm border border-border"
                  onClick={(e) => e.stopPropagation()}
                >
                  <Map size={14} />
                  Start Journey
                </Link>
                <Link
                  href={`/recipes/plan`}
                  className="flex items-center justify-center gap-2 flex-1 py-2 rounded-lg bg-bg-elevated text-text-secondary font-medium hover:text-gold transition-colors text-sm border border-border"
                  onClick={(e) => e.stopPropagation()}
                >
                  <ClipboardList size={14} />
                  Add to Plan
                </Link>
              </div>
            </div>
          )}
          {item.source.vendorName && item.source.cost && (
            <p className="text-sm text-text-secondary">
              Sold by{" "}
              <span className="text-amber-400">{item.source.vendorName}</span>{" "}
              for{" "}
              <span className="text-amber-400">
                {item.source.cost.toLocaleString()} {item.source.currency ?? "Gil"}
              </span>
            </p>
          )}
        </div>
      )}

      {/* Your Current Gear (from profile) */}
      {comparison?.currentEquipment && item && (
        <div className="space-y-2">
          <p className="text-xs uppercase tracking-wider text-text-muted font-semibold">
            Your Current Gear
          </p>
          <div className="bg-bg-elevated rounded-lg p-3 space-y-2">
            <div className="flex items-center gap-3">
              <ItemIcon
                iconUrl={comparison.currentEquipment.iconUrl}
                alt={comparison.currentEquipment.name}
                size="md"
              />
              <div className="flex-1 min-w-0">
                <div className="flex items-center gap-2">
                  <p
                    className={`text-sm font-medium truncate ${RARITY_COLOR_CLASS[comparison.currentEquipment.rarityName] ?? "text-text-primary"}`}
                  >
                    {comparison.currentEquipment.name}
                  </p>
                  <span className="text-xs text-text-muted shrink-0">
                    iLv {comparison.currentEquipment.itemLevel}
                  </span>
                </div>
                <div className="flex items-center gap-2 mt-0.5 flex-wrap">
                  {getStatDiffs(item, comparison.currentEquipment, isDoH).map(
                    (d) => (
                      <span
                        key={d.label}
                        className={`text-xs font-medium ${d.diff > 0 ? "text-rarity-uncommon" : "text-red-400"}`}
                      >
                        {d.diff > 0 ? "+" : ""}
                        {d.diff} {d.label}
                      </span>
                    ),
                  )}
                  {getStatDiffs(item, comparison.currentEquipment, isDoH)
                    .length === 0 && (
                    <span className="text-xs text-text-muted">Same stats</span>
                  )}
                </div>
              </div>
            </div>
            {comparison.comparison?.isUpgrade && onApplySingle && (
              <button
                onClick={() =>
                  onApplySingle(
                    comparison.profileSlotKey,
                    item.equipmentId,
                  )
                }
                className="w-full py-2 rounded-lg bg-emerald-500/15 text-emerald-400 text-sm font-medium hover:bg-emerald-500/25 transition-colors"
              >
                Use recommended
              </button>
            )}
          </div>
        </div>
      )}

      {/* Alternatives or Comparison */}
      {alternatives.length > 0 && (
        comparisonItem ? (
          <div className="space-y-2 animate-fade-in">
            <button
              onClick={() => setComparisonItem(null)}
              className="flex items-center gap-1 text-xs text-text-secondary hover:text-gold transition-colors"
            >
              <ArrowLeft size={14} />
              Back to alternatives
            </button>
            <GearComparisonView
              recommended={item}
              selected={comparisonItem}
              isDoH={isDoH}
              onClose={() => setComparisonItem(null)}
            />
          </div>
        ) : (
          <div className="space-y-2">
            <p className="text-xs uppercase tracking-wider text-text-muted font-semibold">
              Other Options
            </p>
            <div className="space-y-2">
              {alternatives.map((alt) => {
                const altRarity =
                  RARITY_COLOR_CLASS[alt.rarityName] ?? "text-text-primary";
                const diffs = getStatDiffs(item, alt, isDoH);

                return (
                  <div
                    key={alt.equipmentId}
                    className="flex items-center gap-3 bg-bg-elevated rounded-lg p-2.5"
                  >
                    <ItemIcon iconUrl={alt.iconUrl} alt={alt.name} size="md" />
                    <div className="flex-1 min-w-0">
                      <div className="flex items-center gap-2">
                        <p className={`text-sm font-medium truncate ${altRarity}`}>
                          {alt.name}
                        </p>
                        <span className="text-xs text-text-muted shrink-0">
                          iLv {alt.itemLevel}
                        </span>
                      </div>
                      <div className="flex items-center gap-2 mt-0.5 flex-wrap">
                        {diffs.map((d) => (
                          <span
                            key={d.label}
                            className={`text-xs font-medium ${d.diff > 0 ? "text-rarity-uncommon" : "text-red-400"}`}
                          >
                            {d.diff > 0 ? "+" : ""}
                            {d.diff} {d.label}
                          </span>
                        ))}
                        {diffs.length === 0 && (
                          <span className="text-xs text-text-muted">
                            Same stats
                          </span>
                        )}
                      </div>
                    </div>
                    <div className="shrink-0 flex items-center gap-2">
                      <button
                        onClick={() => setComparisonItem(alt)}
                        className="p-1.5 rounded-md text-text-muted hover:text-gold hover:bg-gold/10 transition-colors"
                        title="Compare with recommended"
                        aria-label={`Compare ${alt.name} with recommended`}
                      >
                        <ArrowLeftRight size={14} />
                      </button>
                      <div className="text-right">
                        {alt.source && (
                          <SourceBadge sourceType={alt.source.type} />
                        )}
                        <p className="text-xs text-text-muted mt-0.5">
                          {alt.weightedScore.toLocaleString(undefined, {
                            maximumFractionDigits: 0,
                          })}
                        </p>
                      </div>
                    </div>
                  </div>
                );
              })}
            </div>
          </div>
        )
      )}
    </div>
  );
}

// Bottom sheet for mobile
export function BottomSheet({
  children,
  onClose,
}: {
  children: React.ReactNode;
  onClose: () => void;
}) {
  // Body scroll lock
  useEffect(() => {
    const prev = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    return () => {
      document.body.style.overflow = prev;
    };
  }, []);

  // Close on Escape
  useEffect(() => {
    const handler = (e: KeyboardEvent) => {
      if (e.key === "Escape") onClose();
    };
    document.addEventListener("keydown", handler);
    return () => document.removeEventListener("keydown", handler);
  }, [onClose]);

  return createPortal(
    <div className="fixed inset-0 z-50" role="dialog" aria-modal="true" aria-label="Gear details">
      {/* Backdrop */}
      <div
        className="absolute inset-0 bg-black/60 animate-fade-in"
        onClick={onClose}
      />
      {/* Sheet */}
      <div className="absolute bottom-0 left-0 right-0 max-h-[85vh] bg-bg-surface rounded-t-2xl border-t border-border overflow-y-auto animate-slide-up-sheet">
        <div className="sticky top-0 z-10 bg-bg-surface pt-3 pb-2 px-4 border-b border-border flex justify-between items-center">
          <span className="text-xs uppercase tracking-wider text-text-muted">
            Gear Details
          </span>
          <button
            onClick={onClose}
            className="p-1 text-text-muted hover:text-text-primary transition-colors"
            aria-label="Close"
          >
            <X size={20} />
          </button>
        </div>
        <div className="p-4">{children}</div>
      </div>
    </div>,
    document.body,
  );
}
