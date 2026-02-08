"use client";

import Link from "next/link";
import type { ItemDto } from "@/types/api";
import { ITEM_CATEGORY_INFO } from "@/lib/constants";
import { ItemIcon } from "./ItemIcon";
import { Hammer, Shield } from "lucide-react";

interface ItemCardProps {
  item: ItemDto;
}

export function ItemCard({ item }: ItemCardProps) {
  const categoryInfo = ITEM_CATEGORY_INFO[item.category];

  const content = (
    <div className="flex items-center gap-3 p-3 rounded-lg bg-bg-surface hover:bg-bg-elevated border border-border hover:border-border-focus transition-colors">
      <ItemIcon iconUrl={item.iconUrl} alt={item.name} size="md" />
      <div className="flex-1 min-w-0">
        <div className="flex items-center gap-2">
          <span className="text-sm font-medium text-text-primary truncate">
            {item.name}
          </span>
          {item.recipeId !== null && (
            <span title="Craftable">
              <Hammer size={14} className="text-gold shrink-0" />
            </span>
          )}
          {item.isEquipment && (
            <span title="Equipment — View in Gear Browser">
              <Shield size={14} className="text-blue-400 shrink-0" />
            </span>
          )}
        </div>
        <div className="flex items-center gap-2 mt-0.5">
          <span className="text-xs text-text-muted">iLv {item.itemLevel}</span>
          <span
            className={`text-xs px-1.5 py-0.5 rounded-full bg-bg-elevated ${categoryInfo.colorClass}`}
          >
            {categoryInfo.label}
          </span>
        </div>
      </div>
    </div>
  );

  if (item.recipeId !== null) {
    return (
      <Link href={`/recipes/${item.recipeId}`} className="block">
        {content}
      </Link>
    );
  }

  if (item.isEquipment) {
    return (
      <Link
        href={`/gear?mode=search&search=${encodeURIComponent(item.name)}`}
        className="block"
      >
        {content}
      </Link>
    );
  }

  return content;
}
