"use client";

import { useState, useMemo } from "react";
import {
  ChevronDown,
  ChevronRight,
  Store,
  Pickaxe,
  TreePine,
  Fish,
  Gem,
  ArrowUpDown,
} from "lucide-react";
import { ItemLink, SourceBadge } from "@/components/ui";
import { CopyShoppingList } from "./CopyShoppingList";
import { SaveShoppingListButton } from "./SaveShoppingListButton";
import {
  categorizeShoppingList,
  getSourceDetailText,
} from "@/lib/tree-utils";
import { useShoppingListProgress } from "@/hooks/useShoppingListProgress";
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
  recipeId: number;
}

type SortMode = "source" | "name" | "zone";

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
  recipeId,
}: ShoppingListViewProps) {
  const [collapsed, setCollapsed] = useState<Set<string>>(
    () => new Set(["crystals"]),
  );
  const [sortMode, setSortMode] = useState<SortMode>("source");

  const totalItemCount = shoppingList.length + crystalRequirements.length;
  const { isChecked, toggle, progress, checkedCount, clear } =
    useShoppingListProgress(recipeId, totalItemCount);

  const categorized = categorizeShoppingList(shoppingList);
  const vendorItemCount = categorized.vendors.reduce(
    (sum, v) => sum + v.items.length,
    0,
  );

  // Flat sorted lists for non-source sort modes
  const allItems = useMemo(() => [...shoppingList, ...crystalRequirements], [shoppingList, crystalRequirements]);

  const sortedByName = useMemo(() => {
    return [...allItems].sort((a, b) => {
      const ac = isChecked(a.itemId) ? 1 : 0;
      const bc = isChecked(b.itemId) ? 1 : 0;
      if (ac !== bc) return ac - bc;
      return a.itemName.localeCompare(b.itemName);
    });
  }, [allItems, isChecked]);

  const sortedByZone = useMemo(() => {
    const groups = new Map<string, ShoppingListItemDto[]>();
    for (const item of allItems) {
      const gatherSource = item.sources.find((s) => s.zone);
      const zone = gatherSource?.zone ?? item.primarySource ?? "Other";
      if (!groups.has(zone)) groups.set(zone, []);
      groups.get(zone)!.push(item);
    }
    return Array.from(groups.entries()).sort(([a], [b]) => a.localeCompare(b));
  }, [allItems]);

  function toggleSection(key: string) {
    setCollapsed((prev) => {
      const next = new Set(prev);
      if (next.has(key)) next.delete(key);
      else next.add(key);
      return next;
    });
  }

  // Sort checked items to the bottom within any list
  function sortCheckedBottom<T extends ShoppingListItemDto>(items: T[]): T[] {
    return [...items].sort((a, b) => {
      const ac = isChecked(a.itemId) ? 1 : 0;
      const bc = isChecked(b.itemId) ? 1 : 0;
      return ac - bc;
    });
  }

  return (
    <div className="relative pb-20">
      {/* Progress bar */}
      {checkedCount > 0 && (
        <div className="h-1 bg-bg-elevated rounded-full overflow-hidden mb-2">
          <div
            className="h-full bg-emerald-500 transition-all duration-300"
            style={{ width: `${progress * 100}%` }}
          />
        </div>
      )}

      {/* Sort dropdown */}
      <div className="flex items-center justify-between mb-3">
        <div className="flex items-center gap-2 text-xs text-text-muted">
          {checkedCount > 0 && (
            <>
              <span className="text-emerald-400 font-medium">
                {checkedCount}/{totalItemCount} collected
              </span>
              <button
                onClick={clear}
                className="text-text-muted hover:text-red-400 transition-colors"
              >
                Reset
              </button>
            </>
          )}
        </div>
        <div className="flex items-center gap-1.5">
          <ArrowUpDown size={12} className="text-text-muted" aria-hidden="true" />
          <select
            aria-label="Sort shopping list"
            value={sortMode}
            onChange={(e) => setSortMode(e.target.value as SortMode)}
            className="text-xs bg-bg-elevated border border-border rounded px-2 py-1 text-text-secondary"
          >
            <option value="source">By Source</option>
            <option value="name">By Name</option>
            <option value="zone">By Zone</option>
          </select>
        </div>
      </div>

      {/* Source-based layout (default) */}
      {sortMode === "source" && (
        <div className="space-y-4">
          {crystalRequirements.length > 0 && (
            <CollapsibleSection
              sectionKey="crystals"
              title="Crystals"
              count={crystalRequirements.length}
              icon={<Gem size={16} className="text-violet-400" />}
              isCollapsed={collapsed.has("crystals")}
              onToggle={() => toggleSection("crystals")}
            >
              <ScrollContainer itemCount={crystalRequirements.length}>
                <div className="divide-y divide-border/50">
                  {sortCheckedBottom(crystalRequirements).map((item) => (
                    <ShoppingRow
                      key={item.itemId}
                      item={item}
                      checked={isChecked(item.itemId)}
                      onToggle={() => toggle(item.itemId)}
                    />
                  ))}
                </div>
              </ScrollContainer>
            </CollapsibleSection>
          )}

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
              onToggle={() => toggleSection("gathered")}
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
                    <ScrollContainer itemCount={group.items.length}>
                      <div className="divide-y divide-border/50">
                        {sortCheckedBottom(group.items).map((item) => (
                          <ShoppingRow
                            key={item.itemId}
                            item={item}
                            checked={isChecked(item.itemId)}
                            onToggle={() => toggle(item.itemId)}
                          />
                        ))}
                      </div>
                    </ScrollContainer>
                  </div>
                );
              })}
            </CollapsibleSection>
          )}

          {categorized.vendors.length > 0 && (
            <CollapsibleSection
              sectionKey="vendor"
              title="Vendor Materials"
              count={vendorItemCount}
              icon={<Store size={16} className="text-amber-400" />}
              isCollapsed={collapsed.has("vendor")}
              onToggle={() => toggleSection("vendor")}
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
                    {sortCheckedBottom(vendor.items).map((item) => (
                      <ShoppingRow
                        key={item.itemId}
                        item={item}
                        checked={isChecked(item.itemId)}
                        onToggle={() => toggle(item.itemId)}
                      />
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

          {categorized.other.length > 0 && (
            <CollapsibleSection
              sectionKey="other"
              title="Other Materials"
              count={categorized.other.length}
              isCollapsed={collapsed.has("other")}
              onToggle={() => toggleSection("other")}
            >
              <ScrollContainer itemCount={categorized.other.length}>
                <div className="divide-y divide-border/50">
                  {sortCheckedBottom(categorized.other).map((item) => (
                    <ShoppingRow
                      key={item.itemId}
                      item={item}
                      checked={isChecked(item.itemId)}
                      onToggle={() => toggle(item.itemId)}
                    />
                  ))}
                </div>
              </ScrollContainer>
            </CollapsibleSection>
          )}
        </div>
      )}

      {/* Name-sorted flat list */}
      {sortMode === "name" && (
        <div className="bg-bg-surface rounded-lg border border-border overflow-hidden">
          <ScrollContainer itemCount={sortedByName.length}>
            <div className="divide-y divide-border/50">
              {sortedByName.map((item) => (
                <ShoppingRow
                  key={item.itemId}
                  item={item}
                  checked={isChecked(item.itemId)}
                  onToggle={() => toggle(item.itemId)}
                />
              ))}
            </div>
          </ScrollContainer>
        </div>
      )}

      {/* Zone-grouped list */}
      {sortMode === "zone" && (
        <div className="space-y-4">
          {sortedByZone.map(([zone, items]) => (
            <CollapsibleSection
              key={zone}
              sectionKey={`zone-${zone}`}
              title={zone}
              count={items.length}
              isCollapsed={collapsed.has(`zone-${zone}`)}
              onToggle={() => toggleSection(`zone-${zone}`)}
            >
              <ScrollContainer itemCount={items.length}>
                <div className="divide-y divide-border/50">
                  {sortCheckedBottom(items).map((item) => (
                    <ShoppingRow
                      key={item.itemId}
                      item={item}
                      checked={isChecked(item.itemId)}
                      onToggle={() => toggle(item.itemId)}
                    />
                  ))}
                </div>
              </ScrollContainer>
            </CollapsibleSection>
          ))}
        </div>
      )}

      {/* Sticky summary bar */}
      <div className="fixed bottom-16 left-0 right-0 z-30 bg-bg-surface/95 backdrop-blur border-t border-border px-4 py-3">
        {checkedCount > 0 && (
          <div className="max-w-2xl mx-auto h-1 bg-bg-elevated rounded-full overflow-hidden mb-2">
            <div
              className="h-full bg-emerald-500 transition-all duration-300"
              style={{ width: `${progress * 100}%` }}
            />
          </div>
        )}
        <div className="max-w-2xl mx-auto flex items-center justify-between gap-3">
          <div className="flex flex-col gap-0.5 min-w-0">
            <span className="text-xs text-text-secondary truncate">
              {checkedCount > 0
                ? `${checkedCount}/${totalItemCount} materials collected`
                : `${summary.totalUniqueRawMaterials} materials · ${summary.totalUniqueCrystals} crystals${vendorItemCount > 0 ? ` · ${vendorItemCount} vendor` : ""}`}
            </span>
            {categorized.totalGil > 0 && (
              <span className="text-xs text-amber-400 font-mono">
                ~{categorized.totalGil.toLocaleString()} Gil
              </span>
            )}
          </div>
          <div className="flex items-center gap-2">
            <SaveShoppingListButton
              recipeId={recipeId}
              itemName={itemName}
              quantity={quantity}
            />
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
    </div>
  );
}

// --- Scroll container for large sections ---

function ScrollContainer({
  itemCount,
  children,
}: {
  itemCount: number;
  children: React.ReactNode;
}) {
  if (itemCount > 50) {
    return <div className="max-h-[400px] overflow-y-auto">{children}</div>;
  }
  return <>{children}</>;
}

// --- Collapsible Section ---

function CollapsibleSection({
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

function ShoppingRow({
  item,
  checked,
  onToggle,
}: {
  item: ShoppingListItemDto;
  checked: boolean;
  onToggle: () => void;
}) {
  const sourceDetail = getSourceDetailText(item);

  return (
    <div
      className={`flex items-center gap-3 px-4 py-2.5 min-h-[44px] ${checked ? "opacity-60" : ""}`}
    >
      <button
        onClick={onToggle}
        className="shrink-0 w-[44px] h-[44px] flex items-center justify-center"
        aria-label={checked ? "Uncheck item" : "Check item"}
      >
        <div
          className={`w-5 h-5 rounded border-2 flex items-center justify-center transition-colors ${
            checked
              ? "bg-emerald-500 border-emerald-500"
              : "border-border hover:border-text-muted"
          }`}
        >
          {checked && (
            <svg viewBox="0 0 12 12" className="w-3 h-3 text-white" fill="none" stroke="currentColor" strokeWidth={2}>
              <path d="M2 6l3 3 5-5" />
            </svg>
          )}
        </div>
      </button>
      <div className="flex-1 min-w-0">
        <ItemLink
          itemId={item.itemId}
          itemName={item.itemName}
          iconUrl={item.iconUrl}
          compact
          className={`truncate block ${checked ? "line-through" : ""}`}
        />
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
          x{item.totalQuantity}
        </span>
      </div>
    </div>
  );
}
