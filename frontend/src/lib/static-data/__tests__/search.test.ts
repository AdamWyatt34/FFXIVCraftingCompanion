import { describe, expect, it } from "vitest";
import type { ItemSearchResultDto } from "@/types/api";
import type { CompactSearchIndex, ItemExport, RecipeExport, SearchIndexEntry } from "../types";
import { QUICK_FILTERS, searchIndex } from "../search";
import { inflateSearchIndex } from "../search-compact";
import { readGoldenJson, readGoldenLines } from "./goldens";

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
  const classJobLevel = new Map(
    readGoldenLines<RecipeExport>("recipe-export.jsonl").map((r) => [r.recipe.recipeId, r.craftingParameters.classJobLevel]),
  );
  return readGoldenLines<ItemExport>("item-export.jsonl").map(({ item, context, equipment }) => ({
    itemId: item.itemId,
    name: item.name,
    icon: Number(/%2F(\d{6})\.tex/.exec(item.iconUrl)![1]),
    itemLevel: item.itemLevel,
    category: item.category,
    canBeHQ: item.canBeHQ,
    recipes: context.recipes.map((r) => ({ recipeId: r.recipeId, craftType: r.craftType, level: classJobLevel.get(r.recipeId) ?? 0 })),
    equipment: equipment ? { slot: "", equipLevel: equipment.equipLevel, classJobCategory: equipment.classJobCategory } : null,
  }));
}

function entry(overrides: Partial<SearchIndexEntry> & { itemId: number; name: string }): SearchIndexEntry {
  return { icon: 0, itemLevel: 1, category: "Other", canBeHQ: false, recipes: [], equipment: null, ...overrides };
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

describe("quick filters", () => {
  const index: SearchIndexEntry[] = [
    entry({ itemId: 1, name: "Claro Walnut Lumber", category: "Material", recipes: [{ recipeId: 10, craftType: "Carpenter", level: 98 }] }),
    entry({ itemId: 2, name: "Dark Chestnut Lumber", category: "Material", recipes: [{ recipeId: 11, craftType: "Carpenter", level: 90 }] }),
    entry({ itemId: 3, name: "Ra'Kaznar Saw", recipes: [{ recipeId: 12, craftType: "Blacksmith", level: 100 }], equipment: { slot: "MainHand", equipLevel: 100, classJobCategory: "CRP" } }),
    entry({ itemId: 4, name: "Saw of Stars", equipment: { slot: "MainHand", equipLevel: 100, classJobCategory: "CRP" } }),
    entry({ itemId: 5, name: "Indagator's Saw", recipes: [{ recipeId: 13, craftType: "Blacksmith", level: 90 }], equipment: { slot: "MainHand", equipLevel: 90, classJobCategory: "CRP" } }),
    entry({ itemId: 6, name: "Fire Crystal", category: "Other" }),
  ];
  const names = (filter: keyof typeof QUICK_FILTERS, query: string | null = null) =>
    searchIndex(index, query, 1, 20, null, filter).items.map((i) => i.name);

  it("Dawntrail Recipes are the level 91 to 100 ones", () => {
    expect(names("dawntrail")).toEqual(["Claro Walnut Lumber", "Ra'Kaznar Saw"]);
  });

  it("Level 100 Gear is crafted equipment with equip level 100", () => {
    expect(names("level100Gear")).toEqual(["Ra'Kaznar Saw"]);
  });

  it("Common Materials are the items classified as materials", () => {
    expect(names("materials")).toEqual(["Claro Walnut Lumber", "Dark Chestnut Lumber"]);
  });

  it("combines with a text query and reports the filtered total", () => {
    const result = searchIndex(index, "walnut", 1, 20, null, "materials");
    expect(result.items.map((i) => i.name)).toEqual(["Claro Walnut Lumber"]);
    expect(result.totalCount).toBe(1);
  });
});

describe("compact index", () => {
  const compact = readGoldenJson<CompactSearchIndex>("search-compact.json");
  const inflated = inflateSearchIndex(compact);
  const cases = readGoldenLines<ItemSearchGolden>("item-search.jsonl");

  it("rebuilds the exporter's entries", () => {
    // The item-export golden carries no equipment slot and no class job level for recipes outside
    // the fixture roots, so those two fields stay out of the comparison.
    const stable = (entries: SearchIndexEntry[]) =>
      entries.map((e) => ({
        itemId: e.itemId,
        name: e.name,
        icon: e.icon,
        itemLevel: e.itemLevel,
        category: e.category,
        canBeHQ: e.canBeHQ,
        recipes: e.recipes.map((r) => `${r.recipeId}:${r.craftType}`).sort(),
        equipment: e.equipment ? `${e.equipment.equipLevel}:${e.equipment.classJobCategory}` : null,
      }));
    expect(stable(inflated)).toEqual(stable(indexFromGolden()));
    expect(compact.slots.length).toBeGreaterThan(0);
    expect(compact.categories.length).toBeLessThan(compact.itemIds.length);
  });

  it.each(cases.map((c) => [`${c.query ?? "(none)"} ${c.craftType ?? ""} p${c.page}/${c.pageSize}`, c] as const))(
    "matches the C# handler for %s through the compact index",
    (_name, golden) => {
      expect(searchIndex(inflated, golden.query, golden.page, golden.pageSize, golden.craftType)).toEqual(golden.result);
    },
  );
});
