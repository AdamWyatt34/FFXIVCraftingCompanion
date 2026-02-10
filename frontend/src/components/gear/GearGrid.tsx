"use client";

import { useState, useMemo, Fragment } from "react";
import { Hammer } from "lucide-react";
import { GearSlotCard } from "./GearSlotCard";
import {
  GearSlotExpandedContent,
  BottomSheet,
} from "./GearSlotExpanded";
import { useMediaQuery } from "@/hooks/useMediaQuery";
import { getSlotGroup, type SlotGroup } from "@/lib/gear-constants";
import type { GearSlotDto } from "@/types/gear";
import type { SlotComparisonDto } from "@/types/gear-comparison";

interface GearGridProps {
  slots: GearSlotDto[];
  isDoH: boolean;
  slotComparisons?: SlotComparisonDto[];
  onApplySingle?: (slotKey: string, equipmentId: number) => void;
}

const SECTION_ORDER: SlotGroup[] = ["Weapons", "Armor", "Accessories"];

export function GearGrid({ slots, isDoH, slotComparisons, onApplySingle }: GearGridProps) {
  const [expandedIndex, setExpandedIndex] = useState<number | null>(null);
  const isDesktop = useMediaQuery("(min-width: 640px)");

  const maxScore = useMemo(
    () => Math.max(0, ...slots.map((s) => s.recommendation?.weightedScore ?? 0)),
    [slots],
  );

  const grouped = useMemo(() => {
    const groups: Record<SlotGroup, { slot: GearSlotDto; idx: number }[]> = {
      Weapons: [],
      Armor: [],
      Accessories: [],
    };
    slots.forEach((slot, idx) => {
      const group = getSlotGroup(slot.slotKey);
      groups[group].push({ slot, idx });
    });
    return groups;
  }, [slots]);

  const craftedPieces = useMemo(
    () =>
      slots.filter(
        (s) => s.recommendation?.source?.hasRecipe && s.recommendation.source.recipeId,
      ),
    [slots],
  );

  const handleToggle = (idx: number) => {
    setExpandedIndex((prev) => (prev === idx ? null : idx));
  };

  return (
    <div className="space-y-6">
      {SECTION_ORDER.map((group) => {
        const items = grouped[group];
        if (items.length === 0) return null;
        return (
          <section key={group} className="space-y-3">
            {/* Section divider */}
            <div className="flex items-center gap-3">
              <span className="h-px flex-1 bg-border" />
              <h3 className="text-xs uppercase tracking-wider text-text-muted font-semibold">
                {group}
              </h3>
              <span className="h-px flex-1 bg-border" />
            </div>

            {/* Slot cards grid */}
            <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
              {items.map(({ slot, idx }) => {
                const slotComp = slotComparisons?.find(c => c.slotName === slot.slotName) ?? undefined;
                return (
                  <Fragment key={`${slot.slotKey}-${idx}`}>
                    <GearSlotCard
                      slot={slot}
                      isDoH={isDoH}
                      isExpanded={expandedIndex === idx}
                      onToggleExpand={() => handleToggle(idx)}
                      maxScore={maxScore}
                      comparison={slotComp}
                    />
                    {/* Desktop inline expansion */}
                    {isDesktop && expandedIndex === idx && (
                      <div className="col-span-full animate-expand-down rounded-lg border border-border bg-bg-surface p-4">
                        <GearSlotExpandedContent
                          slot={slot}
                          isDoH={isDoH}
                          onClose={() => setExpandedIndex(null)}
                          comparison={slotComp}
                          onApplySingle={onApplySingle}
                        />
                      </div>
                    )}
                  </Fragment>
                );
              })}
            </div>
          </section>
        );
      })}

      {/* Craft This Gear banner */}
      {craftedPieces.length > 0 && (
        <div className="rounded-lg border border-gold/30 bg-gold/5 p-4 flex items-center justify-between gap-4">
          <div className="flex items-center gap-3">
            <Hammer size={20} className="text-gold shrink-0" />
            <div>
              <p className="text-sm font-medium text-gold">
                {craftedPieces.length} piece
                {craftedPieces.length > 1 ? "s" : ""} can be crafted
              </p>
              <p className="text-xs text-text-muted mt-0.5">
                View combined material list for all craftable gear
              </p>
            </div>
          </div>
          <span
            className="shrink-0 px-4 py-2 rounded-lg bg-gold/20 text-gold text-sm font-medium opacity-50 cursor-not-allowed"
            title="Combined materials view coming soon"
          >
            View Materials
          </span>
        </div>
      )}

      {/* Mobile bottom sheet */}
      {!isDesktop && expandedIndex !== null && slots[expandedIndex] && (
        <BottomSheet onClose={() => setExpandedIndex(null)}>
          <GearSlotExpandedContent
            slot={slots[expandedIndex]}
            isDoH={isDoH}
            onClose={() => setExpandedIndex(null)}
            comparison={slotComparisons?.find(c => c.slotName === slots[expandedIndex].slotName)}
            onApplySingle={onApplySingle}
          />
        </BottomSheet>
      )}
    </div>
  );
}
