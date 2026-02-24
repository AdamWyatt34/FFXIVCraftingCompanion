"use client";

import Link from "next/link";
import { FlaskConical } from "lucide-react";
import { ItemIcon } from "@/components/ui";
import { CRAFT_TYPE_INFO } from "@/lib/constants";
import type { AggregatedIntermediateCraftDto, CraftPlanItemResultDto } from "@/types/craft-plan";
import type { CraftType } from "@/types/api";

interface PlanCraftingOrderProps {
  intermediateCrafts: AggregatedIntermediateCraftDto[];
  finalItems: CraftPlanItemResultDto[];
}

export function PlanCraftingOrder({ intermediateCrafts, finalItems }: PlanCraftingOrderProps) {
  const sorted = [...intermediateCrafts].sort((a, b) => a.craftOrder - b.craftOrder);

  return (
    <div className="space-y-3">
      {/* Intermediate crafts */}
      {sorted.length > 0 && (
        <div className="bg-bg-surface rounded-lg border border-border overflow-hidden">
          <div className="px-4 py-2.5 bg-bg-elevated/50">
            <span className="text-sm font-semibold text-text-secondary uppercase tracking-wider">
              Intermediate Crafts ({sorted.length})
            </span>
          </div>
          <div className="divide-y divide-border/50">
            {sorted.map((craft, i) => {
              const craftInfo = CRAFT_TYPE_INFO[craft.craftType as CraftType];
              return (
                <div key={craft.itemId} className="flex items-center gap-3 px-4 py-2.5 min-h-[44px]">
                  <span className="text-xs text-text-muted font-mono w-6">{i + 1}.</span>
                  <ItemIcon iconUrl={craft.iconUrl} alt={craft.itemName} size="sm" />
                  <div className="flex-1 min-w-0">
                    <span className="text-sm text-text-primary truncate block">{craft.itemName}</span>
                    <span className={`text-xs ${craftInfo?.colorClass ?? "text-text-muted"}`}>
                      {craftInfo?.name ?? craft.craftType} Lv.{craft.recipeLevel}
                    </span>
                  </div>
                  <span className="text-sm text-text-muted font-mono">x{craft.totalQuantity}</span>
                  <Link
                    href={`/simulator?recipeId=${craft.recipeId}`}
                    className="p-1.5 rounded text-text-muted hover:text-gold transition-colors"
                  >
                    <FlaskConical size={14} />
                  </Link>
                </div>
              );
            })}
          </div>
        </div>
      )}

      {/* Final items */}
      <div className="bg-bg-surface rounded-lg border border-border overflow-hidden">
        <div className="px-4 py-2.5 bg-bg-elevated/50">
          <span className="text-sm font-semibold text-text-secondary uppercase tracking-wider">
            Final Crafts ({finalItems.length})
          </span>
        </div>
        <div className="divide-y divide-border/50">
          {finalItems.map((item) => {
            const craftInfo = CRAFT_TYPE_INFO[item.craftType as CraftType];
            return (
              <div key={item.recipeId} className="flex items-center gap-3 px-4 py-2.5 min-h-[44px]">
                <ItemIcon iconUrl={item.iconUrl} alt={item.itemName} size="sm" />
                <div className="flex-1 min-w-0">
                  <span className="text-sm text-text-primary truncate block font-medium">{item.itemName}</span>
                  <span className={`text-xs ${craftInfo?.colorClass ?? "text-text-muted"}`}>
                    {craftInfo?.name ?? item.craftType}
                  </span>
                </div>
                <span className="text-sm text-gold font-mono font-bold">x{item.quantity}</span>
                <Link
                  href={`/simulator?recipeId=${item.recipeId}`}
                  className="p-1.5 rounded text-text-muted hover:text-gold transition-colors"
                >
                  <FlaskConical size={14} />
                </Link>
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );
}
