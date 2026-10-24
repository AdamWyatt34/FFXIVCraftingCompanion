import type {
  RecipeIngredientDto,
  IntermediateCraftDto,
  ItemSourceDto,
  MaterialTreeNodeDto,
  MaterialTreeResultDto,
  RecipeSummaryDto,
  ShoppingListItemDto,
} from "@/types/api";
import type { MaterialGraphExport, MaterialItemExport, MaterialSkeletonNode } from "./types";

// Port of RecipeTreeResolver (quantity expansion, aggregation) and MaterialTreeMapper over the
// exported skeleton. Proven by goldens/material-tree.jsonl for quantities 1, 2, 7, 99 and 999.

export type CrystalTable = Record<string, MaterialItemExport>;

export const CRYSTAL_MIN_ID = 2;
export const CRYSTAL_MAX_ID = 19;

export function isCrystal(itemId: number): boolean {
  return itemId >= CRYSTAL_MIN_ID && itemId <= CRYSTAL_MAX_ID;
}

export function craftsNeeded(quantity: number, yieldPerCraft: number): number {
  const perCraft = Math.max(yieldPerCraft, 1);
  return Math.floor((quantity + perCraft - 1) / perCraft);
}

// string.Compare(a, b, StringComparison.OrdinalIgnoreCase): simple per-character upper-casing,
// then UTF-16 code unit order.
export function compareOrdinalIgnoreCase(a: string, b: string): number {
  const x = foldCase(a);
  const y = foldCase(b);
  return x < y ? -1 : x > y ? 1 : 0;
}

function foldCase(s: string): string {
  let out = "";
  for (const ch of s) {
    const upper = ch.toUpperCase();
    out += upper.length === ch.length ? upper : ch;
  }
  return out;
}

// RecipeTreeResolver.PickPrimarySourceType over the exported (display-named) source types.
function pickPrimarySource(sources: ItemSourceDto[]): string {
  if (sources.length === 0) return "Gathered";
  if (sources.some((s) => s.type === "Gathered")) return "Gathered";
  if (sources.some((s) => s.type === "Vendor")) return "Vendor";
  if (sources.some((s) => s.type === "Crafted")) return "Crafted";
  return sources[0].type;
}

function sortCategory(itemId: number, primarySource: string): string {
  if (isCrystal(itemId)) return "Crystals";
  switch (primarySource) {
    case "Gathered":
    case "Vendor":
    case "Mob Drop":
      return primarySource;
    default:
      return "Other";
  }
}

interface Aggregation {
  shopping: Map<number, ShoppingListItemDto>;
  intermediates: { depth: number; item: ShoppingListItemDto; recipe: RecipeSummaryDto }[];
  maxDepth: number;
}

export function expandMaterialTree(
  graph: MaterialGraphExport,
  crystals: CrystalTable,
  quantity: number,
): MaterialTreeResultDto {
  const itemInfo = (itemId: number): MaterialItemExport => {
    const info = isCrystal(itemId) ? crystals[String(itemId)] : graph.items[String(itemId)];
    if (!info) throw new Error(`Material data for item ${itemId} is missing.`);
    return info;
  };

  const build = (node: MaterialSkeletonNode, qty: number, depth: number): MaterialTreeNodeDto => {
    const info = itemInfo(node.itemId);
    const crystal = isCrystal(node.itemId);
    const crafted = !crystal && !node.truncated && info.recipe !== null;

    let children: MaterialTreeNodeDto[] = [];
    if (crafted) {
      const crafts = craftsNeeded(qty, info.recipe!.yield);
      children = node.children.map((child) => build(child, child.perCraft * crafts, depth + 1));
    }

    const primarySource = crafted
      ? "Crafted"
      : node.truncated
        ? pickPrimarySource(info.sources)
        : info.primarySource;

    return {
      itemId: node.itemId,
      itemName: info.itemName,
      iconUrl: info.iconUrl,
      quantity: qty,
      isRawMaterial: !crafted,
      primarySource,
      sources: info.sources,
      recipe: crafted ? info.recipe : null,
      children,
      depth,
    };
  };

  const tree = build(graph.tree, quantity, 0);

  const agg: Aggregation = { shopping: new Map(), intermediates: [], maxDepth: 0 };
  collect(tree, agg);
  const intermediateDemand = new Map<number, number>();
  const rawDemand = addRootDemand(tree, intermediateDemand);
  for (const item of agg.shopping.values()) item.totalQuantity = rawDemand.get(item.itemId) ?? 0;
  for (const { item } of agg.intermediates) item.totalQuantity = intermediateDemand.get(item.itemId) ?? 0;

  const crystalList: ShoppingListItemDto[] = [];
  const shoppingList: ShoppingListItemDto[] = [];
  for (const item of agg.shopping.values()) {
    if (isCrystal(item.itemId)) crystalList.push({ ...item, category: "Crystals" });
    else shoppingList.push(item);
  }
  crystalList.sort((a, b) => compareOrdinalIgnoreCase(a.itemName, b.itemName));
  shoppingList.sort(
    (a, b) => compareOrdinalIgnoreCase(a.category, b.category) || compareOrdinalIgnoreCase(a.itemName, b.itemName),
  );

  // Array.prototype.sort is stable, like OrderByDescending: equal depths keep first-occurrence order.
  const intermediates = [...agg.intermediates].sort((a, b) => b.depth - a.depth);
  const intermediateCrafts: IntermediateCraftDto[] = intermediates.map(({ item, recipe }, i) => ({
    itemId: item.itemId,
    itemName: item.itemName,
    iconUrl: item.iconUrl,
    craftType: recipe.craftType,
    craftTypeName: recipe.craftTypeName,
    recipeId: recipe.recipeId,
    quantity: item.totalQuantity,
    recipeLevel: recipe.recipeLevel,
    craftOrder: i + 1,
  }));

  return {
    tree,
    shoppingList,
    intermediateCrafts,
    crystalRequirements: crystalList,
    summary: {
      totalUniqueRawMaterials: shoppingList.length,
      totalUniqueCrystals: crystalList.length,
      totalIntermediateCrafts: intermediateCrafts.length,
      maxTreeDepth: agg.maxDepth,
      requestedQuantity: quantity,
    },
  };
}

// RecipeTreeResolver.AttachSourcesAndCollect: pre-order, descending only into crafted nodes. Each
// item is listed at its first occurrence; addRootDemand supplies the totals.
function collect(node: MaterialTreeNodeDto, agg: Aggregation): void {
  if (node.depth > agg.maxDepth) agg.maxDepth = node.depth;

  if (node.isRawMaterial) {
    if (!agg.shopping.has(node.itemId)) {
      agg.shopping.set(node.itemId, {
        itemId: node.itemId,
        itemName: node.itemName,
        iconUrl: node.iconUrl,
        totalQuantity: 0,
        primarySource: node.primarySource,
        sources: node.sources,
        category: sortCategory(node.itemId, node.primarySource),
        alsoAvailableFromVendor:
          node.primarySource !== "Vendor" && node.sources.some((s) => s.type === "Vendor"),
      });
    }
    return;
  }

  if (node.depth > 0 && !agg.intermediates.some((x) => x.item.itemId === node.itemId)) {
    agg.intermediates.push({
      depth: node.depth,
      recipe: node.recipe!,
      item: {
        itemId: node.itemId,
        itemName: node.itemName,
        iconUrl: node.iconUrl,
        totalQuantity: 0,
        primarySource: "Crafted",
        sources: node.sources,
        category: "Crafted",
        alsoAvailableFromVendor: false,
      },
    });
  }

  for (const child of node.children) collect(child, agg);
}

// RecipeTreeResolver.AddRootDemand: the root's raw demand by item id. Intermediate demand accumulates
// in the shared map, so spare yield carries over across occurrences and plan roots.
export function addRootDemand(root: MaterialTreeNodeDto, intermediateDemand: Map<number, number>): Map<number, number> {
  const rawDemand = new Map<number, number>();
  if (root.isRawMaterial) {
    rawDemand.set(root.itemId, root.quantity);
    return rawDemand;
  }

  const addDemand = (node: MaterialTreeNodeDto, demand: number): void => {
    if (node.isRawMaterial) {
      rawDemand.set(node.itemId, (rawDemand.get(node.itemId) ?? 0) + demand);
      return;
    }
    const yieldPerCraft = node.recipe!.yield;
    const before = intermediateDemand.get(node.itemId) ?? 0;
    intermediateDemand.set(node.itemId, before + demand);
    const addedCrafts = craftsNeeded(before + demand, yieldPerCraft) - craftsNeeded(before, yieldPerCraft);
    // Child quantities are per occurrence; dividing by its crafts recovers the per-craft amount.
    const occurrenceCrafts = craftsNeeded(node.quantity, yieldPerCraft);
    for (const child of node.children) {
      const perCraft = occurrenceCrafts === 0 ? 0 : Math.floor(child.quantity / occurrenceCrafts);
      addDemand(child, perCraft * addedCrafts);
    }
  };

  for (const child of root.children) addDemand(child, child.quantity);
  return rawDemand;
}

export interface DirectIngredientSummary {
  itemId: number;
  itemName: string;
  iconUrl: string;
  quantity: number;
  canBeHQ: boolean;
  /** "Crafted", "Gathered", "Vendor", "Crystal", ... as the export classifies it. */
  primarySource: string;
  isCrafted: boolean;
  /** What one craft of a crafted ingredient needs: the first level of the material tree below it. */
  madeFrom: { itemId: number; itemName: string; quantity: number }[];
}

/**
 * The recipe's direct ingredients with the first level of the material tree beneath each crafted
 * one, from the export's material graph. Pure, so the recipe page can render it on the server.
 */
export function directIngredients(
  ingredients: RecipeIngredientDto[],
  graph: MaterialGraphExport | null | undefined,
  crystals: CrystalTable,
): DirectIngredientSummary[] {
  const nodes = new Map<number, MaterialSkeletonNode>();
  for (const child of graph?.tree.children ?? []) nodes.set(child.itemId, child);

  const info = (itemId: number): MaterialItemExport | undefined =>
    isCrystal(itemId) ? crystals[String(itemId)] : graph?.items[String(itemId)];

  return ingredients.map((ingredient) => {
    const node = nodes.get(ingredient.itemId);
    const item = info(ingredient.itemId);
    const crystal = isCrystal(ingredient.itemId);
    const isCrafted = !crystal && !!node && !node.truncated && !!item?.recipe;
    return {
      itemId: ingredient.itemId,
      itemName: ingredient.itemName,
      iconUrl: ingredient.iconUrl,
      quantity: ingredient.quantity,
      canBeHQ: ingredient.canBeHQ,
      primarySource: crystal ? "Crystal" : isCrafted ? "Crafted" : item?.primarySource ?? "Other",
      isCrafted,
      madeFrom: isCrafted
        ? node!.children.map((child) => ({
            itemId: child.itemId,
            itemName: info(child.itemId)?.itemName ?? `Item ${child.itemId}`,
            quantity: child.perCraft,
          }))
        : [],
    };
  });
}
