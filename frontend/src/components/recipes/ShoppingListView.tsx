"use client";

import { useState } from "react";
import Link from "next/link";
import {
  ChevronDown,
  ChevronRight,
  Store,
  Pickaxe,
  TreePine,
  Fish,
  Gem,
} from "lucide-react";
import { ItemIcon, SourceBadge } from "@/components/ui";
import { CopyShoppingList } from "./CopyShoppingList";
import {
  categorizeShoppingList,
  getSourceDetailText,
} from "@/lib/tree-utils";
import type {
  ShoppingListItemDto,
  IntermediateCraftDto,
  TreeSummaryDto,
} from "@/types/api";

interface ShoppingListViewProps {
  shoppingList: ShoppingListItemDto[];
  crystalRequirements: ShoppingListItemDto[];
  intermediateCrafts: IntermediateCraftDto[];
  itemName: string;
  quantity: number;
  summary: TreeSummaryDto;
}

const GATHERING_CLASS_ICONS: Record<
  string,
  { icon: typeof Pickaxe; label: string }
> = {
  Miner: { icon: Pickaxe, label: "Mining" },
  Botanist: { icon: TreePine, label: "Botany" },
  Fisher: { icon: Fish, label: "Fishing" },
};

export function ShoppingListView({
  shoppingList,
  crystalRequirements,
  intermediateCrafts,
  itemName,
  quantity,
  summary,
}: ShoppingListViewProps) {
  // Crystals start collapsed
  const [collapsed, setCollapsed] = useState<Set<string>>(
    () => new Set(["crystals"]),
  );

  const categorized = categorizeShoppingList(shoppingList);
  const vendorItemCount = categorized.vendors.reduce(
    (sum, v) => sum + v.items.length,
    0,
  );

  function toggle(key: string) {
    setCollapsed((prev) => {
      const next = new Set(prev);
      if (next.has(key)) next.delete(key);
      else next.add(key);
      return next;
    });
  }

  return (
    <div className="relative pb-20">
      <div className="space-y-4">
        {/* Crystals */}
        {crystalRequirements.length > 0 && (
          <CollapsibleSection
            sectionKey="crystals"
            title="Crystals"
            count={crystalRequirements.length}
            icon={<Gem size={16} className="text-violet-400" />}
            isCollapsed={collapsed.has("crystals")}
            onToggle={() => toggle("crystals")}
          >
            <div className="divide-y divide-border/50">
              {crystalRequirements.map((item) => (
                <ShoppingRow key={item.itemId} item={item} />
              ))}
            </div>
          </CollapsibleSection>
        )}

        {/* Gathered Materials */}
        {categorized.gathered.length > 0 && (
          <CollapsibleSection
            sectionKey="gathered"
            title="Gathered Materials"
            count={categorized.gathered.reduce(
              (sum, g) => sum + g.items.length,
              0,
            )}
            icon={<Pickaxe size={16} className="text-emerald-400" />}
            isCollapsed={collapsed.has("gathered")}
            onToggle={() => toggle("gathered")}
          >
            {categorized.gathered.map((group) => {
              const classInfo = GATHERING_CLASS_ICONS[group.className];
              const Icon = classInfo?.icon ?? Pickaxe;
              const label = classInfo?.label ?? group.className;
              return (
                <div key={group.className}>
                  <div className="flex items-center gap-2 px-4 py-1.5 bg-bg-primary/30">
                    <Icon size={14} className="text-text-muted" />
                    <span className="text-xs font-semibold text-text-muted uppercase tracking-wider">
                      {label}
                    </span>
                    <span className="text-xs text-text-muted">
                      ({group.items.length})
                    </span>
                  </div>
                  <div className="divide-y divide-border/50">
                    {group.items.map((item) => (
                      <ShoppingRow key={item.itemId} item={item} />
                    ))}
                  </div>
                </div>
              );
            })}
          </CollapsibleSection>
        )}

        {/* Vendor Materials */}
        {categorized.vendors.length > 0 && (
          <CollapsibleSection
            sectionKey="vendor"
            title="Vendor Materials"
            count={vendorItemCount}
            icon={<Store size={16} className="text-amber-400" />}
            isCollapsed={collapsed.has("vendor")}
            onToggle={() => toggle("vendor")}
          >
            {categorized.vendors.map((vendor) => (
              <div key={vendor.npc}>
                <div className="flex items-center justify-between px-4 py-1.5 bg-bg-primary/30">
                  <div className="flex items-center gap-2">
                    <Store size={14} className="text-text-muted" />
                    <span className="text-xs font-semibold text-text-muted">
                      {vendor.npc}
                    </span>
                    {vendor.zone && (
                      <span className="text-xs text-text-muted">
                        — {vendor.zone}
                      </span>
                    )}
                  </div>
                  {vendor.totalGil > 0 && (
                    <span className="text-xs text-amber-400 font-mono">
                      {vendor.totalGil.toLocaleString()} Gil
                    </span>
                  )}
                </div>
                <div className="divide-y divide-border/50">
                  {vendor.items.map((item) => (
                    <ShoppingRow key={item.itemId} item={item} />
                  ))}
                </div>
              </div>
            ))}
            {categorized.totalGil > 0 && (
              <div className="flex items-center justify-between px-4 py-2 bg-bg-elevated/50 border-t border-border">
                <span className="text-xs font-semibold text-text-secondary">
                  Total Vendor Cost
                </span>
                <span className="text-sm font-bold text-amber-400 font-mono">
                  {categorized.totalGil.toLocaleString()} Gil
                </span>
              </div>
            )}
          </CollapsibleSection>
        )}

        {/* Other Materials */}
        {categorized.other.length > 0 && (
          <CollapsibleSection
            sectionKey="other"
            title="Other Materials"
            count={categorized.other.length}
            isCollapsed={collapsed.has("other")}
            onToggle={() => toggle("other")}
          >
            <div className="divide-y divide-border/50">
              {categorized.other.map((item) => (
                <ShoppingRow key={item.itemId} item={item} />
              ))}
            </div>
          </CollapsibleSection>
        )}
      </div>

      {/* Sticky summary bar */}
      <div className="fixed bottom-16 left-0 right-0 z-30 bg-bg-surface/95 backdrop-blur border-t border-border px-4 py-3">
        <div className="max-w-2xl mx-auto flex items-center justify-between gap-3">
          <div className="flex flex-col gap-0.5 min-w-0">
            <span className="text-xs text-text-secondary truncate">
              {summary.totalUniqueRawMaterials} materials
              {" · "}
              {summary.totalUniqueCrystals} crystals
              {vendorItemCount > 0 && ` · ${vendorItemCount} vendor`}
            </span>
            {categorized.totalGil > 0 && (
              <span className="text-xs text-amber-400 font-mono">
                ~{categorized.totalGil.toLocaleString()} Gil
              </span>
            )}
          </div>
          <CopyShoppingList
            shoppingList={shoppingList}
            crystalRequirements={crystalRequirements}
            intermediateCrafts={intermediateCrafts}
            itemName={itemName}
            quantity={quantity}
          />
        </div>
      </div>
    </div>
  );
}

// --- Collapsible Section ---

function CollapsibleSection({
  sectionKey: _key,
  title,
  count,
  icon,
  isCollapsed,
  onToggle,
  children,
}: {
  sectionKey: string;
  title: string;
  count: number;
  icon?: React.ReactNode;
  isCollapsed: boolean;
  onToggle: () => void;
  children: React.ReactNode;
}) {
  return (
    <div className="bg-bg-surface rounded-lg border border-border overflow-hidden">
      <button
        onClick={onToggle}
        className="w-full flex items-center gap-2 px-4 py-3 bg-bg-elevated/50 border-b border-border hover:bg-bg-elevated transition-colors min-h-[44px]"
      >
        {isCollapsed ? (
          <ChevronRight size={16} className="text-text-muted shrink-0" />
        ) : (
          <ChevronDown size={16} className="text-text-muted shrink-0" />
        )}
        {icon}
        <h3 className="text-sm font-semibold text-text-secondary uppercase tracking-wider">
          {title}
        </h3>
        <span className="text-xs text-text-muted font-normal">({count})</span>
      </button>
      {!isCollapsed && children}
    </div>
  );
}

// --- Item Row ---

function ShoppingRow({ item }: { item: ShoppingListItemDto }) {
  const sourceDetail = getSourceDetailText(item);

  return (
    <div className="flex items-center gap-3 px-4 py-2.5 min-h-[44px]">
      <ItemIcon iconUrl={item.iconUrl} alt={item.itemName} size="md" />
      <div className="flex-1 min-w-0">
        <Link
          href={`/recipes?q=${encodeURIComponent(item.itemName)}`}
          className="text-sm text-text-primary hover:text-gold transition-colors truncate block"
        >
          {item.itemName}
        </Link>
        {sourceDetail && (
          <span className="text-xs text-text-muted truncate block">
            {sourceDetail}
          </span>
        )}
      </div>
      <div className="flex items-center gap-2 shrink-0">
        {item.alsoAvailableFromVendor && (
          <span title="Also available from vendor">
            <Store size={14} className="text-amber-400" />
          </span>
        )}
        <SourceBadge sourceType={item.primarySource} />
        <span className="text-sm text-text-primary font-bold font-mono min-w-[40px] text-right">
          ×{item.totalQuantity}
        </span>
      </div>
    </div>
  );
}
