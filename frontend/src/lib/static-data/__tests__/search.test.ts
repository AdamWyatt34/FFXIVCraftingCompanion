import { describe, expect, it } from "vitest";
import type { ItemSearchResultDto } from "@/types/api";
import type { ItemExport, SearchIndexEntry } from "../types";
import { searchIndex } from "../search";
import { readGoldenLines } from "./goldens";

interface ItemSearchGolden {
  query: string | null;
  craftType: string | null;
  page: number;
  pageSize: number;
  result: ItemSearchResultDto;
}

// The fixture's items as the exporter would index them, rebuilt from the item-export golden
// (same ItemDto + context the exporter reads) so the search runs over the same slice the C#
// handler searched.
function indexFromGolden(): SearchIndexEntry[] {
  return readGoldenLines<ItemExport>("item-export.jsonl").map(({ item, context, equipment }) => ({
    itemId: item.itemId,
    name: item.name,
    icon: Number(/%2F(\d{6})\.tex/.exec(item.iconUrl)![1]),
    itemLevel: item.itemLevel,
    category: item.category,
    canBeHQ: item.canBeHQ,
    recipes: context.recipes.map((r) => ({ recipeId: r.recipeId, craftType: r.craftType })),
    equipment: equipment ? { slot: "", equipLevel: equipment.equipLevel, classJobCategory: equipment.classJobCategory } : null,
  }));
}

describe("item search", () => {
  const index = indexFromGolden();
  const cases = readGoldenLines<ItemSearchGolden>("item-search.jsonl");

  it.each(cases.map((c) => [`${c.query ?? "(none)"} ${c.craftType ?? ""} p${c.page}/${c.pageSize}`, c] as const))(
    "matches the C# handler for %s",
    (_name, golden) => {
      const result = searchIndex(index, golden.query, golden.page, golden.pageSize, golden.craftType);
      expect(result).toEqual(golden.result);
    },
  );
});
