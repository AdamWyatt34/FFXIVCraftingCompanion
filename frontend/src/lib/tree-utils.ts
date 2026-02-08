import type {
  MaterialTreeNodeDto,
  ShoppingListItemDto,
  IntermediateCraftDto,
} from "@/types/api";

// --- Types ---

export interface GatheringSubGroup {
  className: string; // "Miner", "Botanist", "Fisher"
  items: ShoppingListItemDto[];
}

export interface VendorGroup {
  npc: string;
  zone: string | null;
  items: ShoppingListItemDto[];
  totalGil: number;
}

export interface CategorizedShoppingList {
  gathered: GatheringSubGroup[];
  vendors: VendorGroup[];
  other: ShoppingListItemDto[];
  totalGil: number;
}

// --- Tree Walking ---

/**
 * BFS walk of the material tree to build a map of itemId → direct children (ingredients).
 * Used by CraftingStepsView to show ingredients for each intermediate craft.
 */
export function buildIngredientMap(
  tree: MaterialTreeNodeDto,
): Map<number, MaterialTreeNodeDto[]> {
  const map = new Map<number, MaterialTreeNodeDto[]>();
  const queue: MaterialTreeNodeDto[] = [tree];

  while (queue.length > 0) {
    const node = queue.shift()!;
    if (node.children.length > 0 && !map.has(node.itemId)) {
      map.set(node.itemId, node.children);
    }
    for (const child of node.children) {
      queue.push(child);
    }
  }

  return map;
}

// --- Shopping List Categorization ---

function getGatheringClassName(item: ShoppingListItemDto): string | null {
  for (const src of item.sources) {
    if (src.gatheringClass) return src.gatheringClass;
  }
  return null;
}

function getGatheringLevel(item: ShoppingListItemDto): number {
  for (const src of item.sources) {
    if (src.gatheringLevel != null) return src.gatheringLevel;
  }
  return 0;
}

function getVendorInfo(
  item: ShoppingListItemDto,
): { npc: string; zone: string | null; cost: number } | null {
  for (const src of item.sources) {
    if (src.type === "Vendor" && src.npc) {
      return { npc: src.npc, zone: src.zone, cost: src.cost ?? 0 };
    }
  }
  return null;
}

export function categorizeShoppingList(
  items: ShoppingListItemDto[],
): CategorizedShoppingList {
  const gatheredMap = new Map<string, ShoppingListItemDto[]>();
  const vendorMap = new Map<string, { npc: string; zone: string | null; items: ShoppingListItemDto[]; totalGil: number }>();
  const other: ShoppingListItemDto[] = [];
  let totalGil = 0;

  for (const item of items) {
    if (item.primarySource === "Gathered") {
      const cls = getGatheringClassName(item) ?? "Other";
      const list = gatheredMap.get(cls) ?? [];
      list.push(item);
      gatheredMap.set(cls, list);
    } else if (item.primarySource === "Vendor") {
      const vendor = getVendorInfo(item);
      const key = vendor?.npc ?? "Unknown Vendor";
      const existing = vendorMap.get(key);
      const itemGil = (vendor?.cost ?? 0) * item.totalQuantity;
      totalGil += itemGil;
      if (existing) {
        existing.items.push(item);
        existing.totalGil += itemGil;
      } else {
        vendorMap.set(key, {
          npc: key,
          zone: vendor?.zone ?? null,
          items: [item],
          totalGil: itemGil,
        });
      }
    } else {
      other.push(item);
    }
  }

  // Sort gathered sub-groups by class name, items within by gathering level
  const classOrder = ["Miner", "Botanist", "Fisher"];
  const gathered: GatheringSubGroup[] = Array.from(gatheredMap.entries())
    .sort(([a], [b]) => {
      const ai = classOrder.indexOf(a);
      const bi = classOrder.indexOf(b);
      return (ai === -1 ? 99 : ai) - (bi === -1 ? 99 : bi);
    })
    .map(([className, groupItems]) => ({
      className,
      items: groupItems.sort(
        (a, b) => getGatheringLevel(a) - getGatheringLevel(b),
      ),
    }));

  const vendors: VendorGroup[] = Array.from(vendorMap.values()).sort(
    (a, b) => a.npc.localeCompare(b.npc),
  );

  return { gathered, vendors, other, totalGil };
}

// --- Source Detail Text ---

export function getSourceDetailText(item: ShoppingListItemDto): string {
  const src = item.sources[0];
  if (!src) return "";

  if (src.type === "Gathered" && src.gatheringClass) {
    const parts: string[] = [];
    if (src.zone) parts.push(`in ${src.zone}`);
    const classAbbr =
      src.gatheringClass === "Miner"
        ? "MIN"
        : src.gatheringClass === "Botanist"
          ? "BTN"
          : src.gatheringClass === "Fisher"
            ? "FSH"
            : src.gatheringClass;
    if (src.gatheringLevel)
      parts.push(`Lv. ${src.gatheringLevel} ${classAbbr}`);
    return parts.length > 0
      ? `${src.description} ${parts.join(" ")}`
      : src.description;
  }

  if (src.type === "Vendor" && src.npc) {
    const parts = [`Sold by ${src.npc}`];
    if (src.cost) parts.push(`for ${src.cost.toLocaleString()} Gil`);
    return parts.join(" ");
  }

  if (src.description) return src.description;
  return "Source info unavailable";
}

// --- Clipboard Formatting ---

type CopyFormat = "detailed" | "compact" | "checkbox";

export function formatShoppingListText(
  format: CopyFormat,
  itemName: string,
  quantity: number,
  crystals: ShoppingListItemDto[],
  shoppingList: ShoppingListItemDto[],
  intermediateCrafts: IntermediateCraftDto[],
): string {
  switch (format) {
    case "detailed":
      return formatDetailed(
        itemName,
        quantity,
        crystals,
        shoppingList,
        intermediateCrafts,
      );
    case "compact":
      return formatCompact(crystals, shoppingList);
    case "checkbox":
      return formatCheckbox(crystals, shoppingList);
  }
}

function formatDetailed(
  itemName: string,
  quantity: number,
  crystals: ShoppingListItemDto[],
  shoppingList: ShoppingListItemDto[],
  intermediateCrafts: IntermediateCraftDto[],
): string {
  const lines: string[] = [];
  lines.push(`=== Shopping List: ${itemName} ×${quantity} ===`);
  lines.push("");

  if (crystals.length > 0) {
    lines.push("-- Crystals --");
    for (const c of crystals) {
      lines.push(`${c.itemName} ×${c.totalQuantity}`);
    }
    lines.push("");
  }

  const categorized = categorizeShoppingList(shoppingList);

  for (const group of categorized.gathered) {
    const label =
      group.className === "Miner"
        ? "Mining"
        : group.className === "Botanist"
          ? "Botany"
          : group.className === "Fisher"
            ? "Fishing"
            : group.className;
    lines.push(`-- Gathered (${label}) --`);
    for (const item of group.items) {
      const src = item.sources[0];
      const detail: string[] = [];
      if (src?.zone) detail.push(src.zone);
      if (src?.gatheringLevel) detail.push(`Lv. ${src.gatheringLevel}`);
      const suffix = detail.length > 0 ? ` (${detail.join(", ")})` : "";
      lines.push(`${item.itemName} ×${item.totalQuantity}${suffix}`);
    }
    lines.push("");
  }

  if (categorized.vendors.length > 0) {
    lines.push("-- Vendor --");
    for (const vendor of categorized.vendors) {
      for (const item of vendor.items) {
        const src = item.sources.find((s) => s.type === "Vendor");
        const costText = src?.cost ? `, ${src.cost.toLocaleString()} Gil each` : "";
        lines.push(
          `${item.itemName} ×${item.totalQuantity} (${vendor.npc}${costText})`,
        );
      }
    }
    lines.push("");
  }

  if (categorized.other.length > 0) {
    lines.push("-- Other --");
    for (const item of categorized.other) {
      lines.push(`${item.itemName} ×${item.totalQuantity}`);
    }
    lines.push("");
  }

  if (intermediateCrafts.length > 0) {
    lines.push("-- Craft These --");
    const sorted = [...intermediateCrafts].sort(
      (a, b) => a.craftOrder - b.craftOrder,
    );
    for (const craft of sorted) {
      lines.push(
        `[${craft.craftType}] ${craft.itemName} ×${craft.quantity} (Lv. ${craft.recipeLevel})`,
      );
    }
    lines.push("");
  }

  if (categorized.totalGil > 0) {
    lines.push(`Total Gil: ~${categorized.totalGil.toLocaleString()}`);
  }
  lines.push("Generated by FFXIV Crafting Companion");

  return lines.join("\n");
}

function formatCompact(
  crystals: ShoppingListItemDto[],
  shoppingList: ShoppingListItemDto[],
): string {
  const all = [...shoppingList, ...crystals];
  return all.map((item) => `${item.itemName} ×${item.totalQuantity}`).join("\n");
}

function formatCheckbox(
  crystals: ShoppingListItemDto[],
  shoppingList: ShoppingListItemDto[],
): string {
  const lines: string[] = [];
  for (const item of shoppingList) {
    lines.push(`[ ] ${item.itemName} ×${item.totalQuantity}`);
  }
  for (const c of crystals) {
    lines.push(`[x] ${c.itemName} ×${c.totalQuantity} (usually in stock)`);
  }
  return lines.join("\n");
}
