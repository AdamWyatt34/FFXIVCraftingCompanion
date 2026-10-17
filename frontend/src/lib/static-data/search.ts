import type { ItemDto, ItemSearchResultDto } from "@/types/api";
import type { SearchIndexEntry } from "./types";
import { iconUrlFromId } from "./icon-url";

// Port of SearchItemsQueryHandler + ItemRepository.SearchByNameAsync: substring match on the
// name, prefix matches first, then by name. Case folding is ASCII-only, which is what the
// SQLite-backed golden (NOCASE) and, for the names the game uses, SQL Server's collation give.
// Proven by tests/FFXIV.Tests/StaticData/TestData/goldens/item-search.jsonl.

function foldAscii(s: string): string {
  return s.replace(/[A-Z]/g, (c) => c.toLowerCase());
}

function compareNames(a: string, b: string): number {
  return a < b ? -1 : a > b ? 1 : 0;
}

/** The recipes page's quick filters; `label` is what the chip and the result header show. */
export const QUICK_FILTERS = {
  dawntrail: { label: "Dawntrail Recipes", minLevel: 91 },
  level100Gear: { label: "Level 100 Gear" },
  materials: { label: "Common Materials" },
} as const;

export type QuickFilter = keyof typeof QUICK_FILTERS;

export function isQuickFilter(value: string | null | undefined): value is QuickFilter {
  return value !== null && value !== undefined && value in QUICK_FILTERS;
}

// Dawntrail recipes are the level 91 to 100 ones; the level is the class job level the index
// carries per recipe. Level 100 gear is crafted equipment with equip level 100. Common
// materials are the items the sync classified as materials.
function matchesQuickFilter(entry: SearchIndexEntry, filter: QuickFilter): boolean {
  switch (filter) {
    case "dawntrail":
      return entry.recipes.some((r) => r.level >= QUICK_FILTERS.dawntrail.minLevel);
    case "level100Gear":
      return entry.equipment?.equipLevel === 100 && entry.recipes.length > 0;
    case "materials":
      return entry.category === "Material";
  }
}

export function searchIndex(
  index: readonly SearchIndexEntry[],
  query: string | null | undefined,
  page: number,
  pageSize: number,
  craftType?: string | null,
  quickFilter?: QuickFilter | null,
): ItemSearchResultDto {
  const hasQuery = query !== null && query !== undefined && query.trim() !== "";
  const needle = hasQuery ? foldAscii(query) : "";

  let matches = index;
  if (craftType) {
    matches = matches.filter((e) => e.recipes.some((r) => r.craftType === craftType));
  }
  if (quickFilter) {
    matches = matches.filter((e) => matchesQuickFilter(e, quickFilter));
  }
  if (hasQuery) {
    matches = matches.filter((e) => foldAscii(e.name).includes(needle));
  }

  const keyed = matches.map((entry) => {
    const folded = foldAscii(entry.name);
    return { entry, folded, prefix: hasQuery && folded.startsWith(needle) ? 0 : 1 };
  });
  keyed.sort(
    (x, y) =>
      x.prefix - y.prefix ||
      compareNames(x.folded, y.folded) ||
      x.entry.itemId - y.entry.itemId,
  );

  const offset = (page - 1) * pageSize;
  const items = keyed.slice(offset, offset + pageSize).map(({ entry }) => toItemDto(entry, craftType));

  return { items, totalCount: keyed.length, page, pageSize };
}

export function toItemDto(entry: SearchIndexEntry, craftType?: string | null): ItemDto {
  const preferred = craftType ? entry.recipes.find((r) => r.craftType === craftType) : undefined;
  const recipe = preferred ?? entry.recipes[0];
  return {
    itemId: entry.itemId,
    name: entry.name,
    iconUrl: iconUrlFromId(entry.icon),
    itemLevel: entry.itemLevel,
    category: entry.category as ItemDto["category"],
    canBeHQ: entry.canBeHQ,
    recipeId: recipe ? recipe.recipeId : null,
    isEquipment: entry.equipment !== null,
  };
}
