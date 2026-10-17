import { describe, expect, it } from "vitest";
import type { EquipmentDto, GearSetDto, StatWeightProfileDto } from "@/types/gear";
import type { GearJobExport, ItemExport, SearchIndexEntry, StatWeightProfileExport } from "../types";
import { availableProfiles, recommendGear, searchGearIndex, toSearchResultDto } from "../gear";
import { readGoldenJson, readGoldenLines } from "./goldens";

interface GearSetGolden { job: string; level: number; profile: string; craftedOnly: boolean; result: GearSetDto }
interface ProfileListGolden { job: string | null; result: StatWeightProfileDto[] }
interface GearSearchGolden { query: string; job: string | null; slot: string | null; minLevel: number | null; maxLevel: number | null; result: EquipmentDto[] }

const profiles = readGoldenJson<StatWeightProfileExport[]>("gear-profiles.json");
const jobs = new Map(readGoldenLines<GearJobExport>("gear-export.jsonl").map((j) => [j.job, j]));
const items = readGoldenLines<ItemExport>("item-export.jsonl");

describe("gear recommendations", () => {
  it.each(readGoldenLines<GearSetGolden>("gear-sets.jsonl").map((g) => [`${g.job} L${g.level} ${g.profile}${g.craftedOnly ? " crafted" : ""}`, g] as const))(
    "matches the C# service for %s",
    (_name, golden) => {
      const result = recommendGear(jobs.get(golden.job)!, profiles, golden.level, golden.profile.toLowerCase(), golden.craftedOnly);
      expect(result).toEqual(golden.result);
    },
  );

  it.each(readGoldenLines<ProfileListGolden>("gear-profile-lists.jsonl").map((g) => [g.job ?? "(default)", g] as const))(
    "lists the profiles for %s like GET /api/gear/profiles",
    (_name, golden) => {
      expect(availableProfiles(profiles, golden.job?.toUpperCase() ?? "CRP")).toEqual(golden.result);
    },
  );
});

describe("equipment search", () => {
  // The index the exporter would build for the fixture's items; equipment slot comes from the
  // item's context link.
  const index: SearchIndexEntry[] = items.map(({ item, context, equipment }) => ({
    itemId: item.itemId,
    name: item.name,
    icon: Number(/%2F(\d{6})\.tex/.exec(item.iconUrl)![1]),
    itemLevel: item.itemLevel,
    category: item.category,
    canBeHQ: item.canBeHQ,
    recipes: context.recipes.map((r) => ({ recipeId: r.recipeId, craftType: r.craftType, level: 0 })),
    equipment: equipment
      ? { slot: context.equipmentInfo!.equipSlot, equipLevel: equipment.equipLevel, classJobCategory: equipment.classJobCategory }
      : null,
  }));
  const byId = new Map(items.map((i) => [i.item.itemId, i]));

  it.each(readGoldenLines<GearSearchGolden>("gear-search.jsonl").map((g) => [`${g.query || "(none)"} ${g.job ?? ""} ${g.slot ?? ""} ${g.minLevel ?? ""}-${g.maxLevel ?? ""}`, g] as const))(
    "matches GET /api/gear/search for %s",
    (_name, golden) => {
      const matches = searchGearIndex(index, golden);
      const result = matches.map((m) => toSearchResultDto(byId.get(m.itemId)!.equipment!));
      expect(result).toEqual(golden.result);
    },
  );
});
