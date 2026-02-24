"use client";

import { useState } from "react";
import { ChevronDown, ChevronRight } from "lucide-react";
import { ItemIcon, SourceBadge } from "@/components/ui";
import type { AggregatedMaterialDto } from "@/types/craft-plan";

interface PlanShoppingListProps {
  rawMaterials: AggregatedMaterialDto[];
  crystals: AggregatedMaterialDto[];
}

export function PlanShoppingList({ rawMaterials, crystals }: PlanShoppingListProps) {
  return (
    <div className="space-y-3">
      {crystals.length > 0 && (
        <MaterialSection title="Crystals" items={crystals} defaultCollapsed />
      )}
      {rawMaterials.length > 0 && (
        <MaterialSection title="Raw Materials" items={rawMaterials} />
      )}
    </div>
  );
}

function MaterialSection({
  title,
  items,
  defaultCollapsed = false,
}: {
  title: string;
  items: AggregatedMaterialDto[];
  defaultCollapsed?: boolean;
}) {
  const [isCollapsed, setIsCollapsed] = useState(defaultCollapsed);

  return (
    <div className="bg-bg-surface rounded-lg border border-border overflow-hidden">
      <button
        onClick={() => setIsCollapsed(!isCollapsed)}
        className="w-full flex items-center gap-2 px-4 py-2.5 bg-bg-elevated/50 hover:bg-bg-elevated transition-colors min-h-[44px]"
      >
        {isCollapsed ? (
          <ChevronRight size={16} className="text-text-muted" />
        ) : (
          <ChevronDown size={16} className="text-text-muted" />
        )}
        <span className="text-sm font-semibold text-text-secondary uppercase tracking-wider">
          {title}
        </span>
        <span className="text-xs text-text-muted">({items.length})</span>
      </button>

      {!isCollapsed && (
        <div className="divide-y divide-border/50">
          {items.map((mat) => (
            <MaterialRow key={mat.itemId} material={mat} />
          ))}
        </div>
      )}
    </div>
  );
}

function MaterialRow({ material }: { material: AggregatedMaterialDto }) {
  const [showContributions, setShowContributions] = useState(false);

  return (
    <div className="px-4 py-2.5">
      <div className="flex items-center gap-3 min-h-[36px]">
        <ItemIcon iconUrl={material.iconUrl} alt={material.itemName} size="sm" />
        <span className="flex-1 text-sm text-text-primary truncate">
          {material.itemName}
        </span>
        <SourceBadge sourceType={material.primarySource} />
        <span className="text-sm text-text-primary font-bold font-mono min-w-[40px] text-right">
          x{material.totalQuantity}
        </span>
        {material.contributions.length > 1 && (
          <button
            onClick={() => setShowContributions(!showContributions)}
            className="text-xs text-text-muted hover:text-gold transition-colors"
          >
            {showContributions ? "Hide" : "Used by..."}
          </button>
        )}
      </div>
      {showContributions && material.contributions.length > 0 && (
        <div className="ml-8 mt-1 space-y-0.5">
          {material.contributions.map((c, i) => (
            <div key={i} className="text-xs text-text-muted">
              {c.itemName}: x{c.quantity}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
