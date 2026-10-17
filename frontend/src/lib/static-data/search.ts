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

export function searchIndex(
  index: readonly SearchIndexEntry[],
  query: string | null | undefined,
  page: number,
  pageSize: number,
  craftType?: string | null,
): ItemSearchResultDto {
  const hasQuery = query !== null && query !== undefined && query.trim() !== "";
  const needle = hasQuery ? foldAscii(query) : "";

  let matches = index;
  if (craftType) {
    matches = matches.filter((e) => e.recipes.some((r) => r.craftType === craftType));
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
