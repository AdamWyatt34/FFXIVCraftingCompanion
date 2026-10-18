import { ApiClientError } from "@/lib/api-errors";
import { SEARCH_COMPACT_PATH, SEARCH_INDEX_PATH } from "./paths";
import type { CompactSearchIndex, SearchIndexEntry } from "./types";

/** The entries a compact index encodes; the inverse of SearchIndexExport.Compact in the exporter. */
export function inflateSearchIndex(compact: CompactSearchIndex): SearchIndexEntry[] {
  const count = compact.itemIds.length;
  const entries = new Array<SearchIndexEntry>(count);
  for (let i = 0; i < count; i++) {
    const gear = compact.equipment[i];
    entries[i] = {
      itemId: compact.itemIds[i],
      name: compact.names[i],
      icon: compact.icons[i],
      itemLevel: compact.itemLevels[i],
      category: compact.categories[compact.categoryIndexes[i]],
      canBeHQ: compact.canBeHQ[i],
      recipes: compact.recipes[i].map(([recipeId, craftType, level]) => ({
        recipeId,
        craftType: compact.craftTypes[craftType],
        level,
      })),
      equipment: gear
        ? { slot: compact.slots[gear[0]], equipLevel: gear[1], classJobCategory: compact.classJobCategories[gear[2]] }
        : null,
    };
  }
  return entries;
}

/**
 * The search index of a data set: the compact file when the export wrote one, else the entries
 * file of an older data set. `fetchJson` takes a path relative to the data set and throws
 * ApiClientError(404) for a file that is not there.
 */
export async function loadSearchIndex(fetchJson: <T>(path: string) => Promise<T>): Promise<SearchIndexEntry[]> {
  try {
    return inflateSearchIndex(await fetchJson<CompactSearchIndex>(SEARCH_COMPACT_PATH));
  } catch (err) {
    if (err instanceof ApiClientError && err.status === 404) return fetchJson<SearchIndexEntry[]>(SEARCH_INDEX_PATH);
    throw err;
  }
}
