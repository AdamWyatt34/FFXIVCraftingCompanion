"use client";

import { Minus, Plus, X, ClipboardList } from "lucide-react";
import { ItemIcon } from "@/components/ui";
import { CRAFT_TYPE_INFO } from "@/lib/constants";
import type { CraftPlanItem } from "@/contexts/CraftPlanContext";
import type { CraftType } from "@/types/api";

interface PlanItemListProps {
  items: CraftPlanItem[];
  onRemove: (recipeId: number) => void;
  onUpdateQuantity: (recipeId: number, qty: number) => void;
}

export function PlanItemList({ items, onRemove, onUpdateQuantity }: PlanItemListProps) {
  if (items.length === 0) {
    return (
      <div className="flex flex-col items-center justify-center py-12 text-center">
        <ClipboardList size={48} className="text-text-muted mb-3" />
        <p className="text-text-secondary text-sm">No items in your craft plan</p>
        <p className="text-text-muted text-xs mt-1">
          Search for recipes above to add them
        </p>
      </div>
    );
  }

  return (
    <div className="space-y-2">
      {items.map((item) => {
        const craftInfo = CRAFT_TYPE_INFO[item.craftType as CraftType];
        return (
          <div
            key={item.recipeId}
            className="flex items-center gap-3 p-3 bg-bg-surface rounded-lg border border-border"
          >
            <ItemIcon iconUrl={item.iconUrl} alt={item.itemName} size="md" />
            <div className="flex-1 min-w-0">
              <p className="text-sm text-text-primary truncate">{item.itemName}</p>
              <p className={`text-xs ${craftInfo?.colorClass ?? "text-text-muted"}`}>
                {craftInfo?.name ?? item.craftType} Lv.{item.recipeLevel}
              </p>
            </div>
            <div className="flex items-center gap-1">
              <button
                onClick={() => onUpdateQuantity(item.recipeId, item.quantity - 1)}
                disabled={item.quantity <= 1}
                className="w-7 h-7 flex items-center justify-center rounded bg-bg-elevated text-text-muted hover:text-gold transition-colors disabled:opacity-30"
              >
                <Minus size={14} />
              </button>
              <span className="w-8 text-center text-sm font-mono text-text-primary">
                {item.quantity}
              </span>
              <button
                onClick={() => onUpdateQuantity(item.recipeId, item.quantity + 1)}
                disabled={item.quantity >= 999}
                className="w-7 h-7 flex items-center justify-center rounded bg-bg-elevated text-text-muted hover:text-gold transition-colors disabled:opacity-30"
              >
                <Plus size={14} />
              </button>
            </div>
            <button
              onClick={() => onRemove(item.recipeId)}
              className="w-7 h-7 flex items-center justify-center rounded bg-bg-elevated text-text-muted hover:text-red-400 transition-colors"
            >
              <X size={14} />
            </button>
          </div>
        );
      })}
    </div>
  );
}
