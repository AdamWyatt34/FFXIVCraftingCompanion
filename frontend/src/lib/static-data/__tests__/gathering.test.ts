import { describe, expect, it } from "vitest";
import type { GatheringGuideDto, GatheringGuideItemInput } from "@/types/gathering";
import type { ItemExport } from "../types";
import { buildGatheringGuide, formatTimeUntil, validateGuideItems } from "../gathering";
import { getEorzeanHour, isWindowActive, timeUntilWindowSeconds } from "@/lib/eorzean-time";
import { readGoldenJson, readGoldenLines } from "./goldens";

interface GatheringGuideGolden { name: string; items: GatheringGuideItemInput[]; currentHour: number; result: GatheringGuideDto }
interface EorzeanGolden {
  hours: { unixSeconds: number; hour: number }[];
  windows: { startHour: number; endHour: number; currentHour: number; isActive: boolean; secondsUntil: number; timeUntil: string }[];
}

describe("gathering guide", () => {
  const items = new Map(readGoldenLines<ItemExport>("item-export.jsonl").map((i) => [i.item.itemId, i]));
  const lookup = (id: number) => {
    const data = items.get(id);
    return data ? { item: data.item, gatheringNodes: data.gatheringNodes } : null;
  };

  it.each(readGoldenLines<GatheringGuideGolden>("gathering-guide.jsonl").map((g) => [`${g.name} at ${g.currentHour}:00`, g] as const))(
    "matches the C# service for %s",
    (_name, golden) => {
      expect(buildGatheringGuide(golden.items, lookup, golden.currentHour)).toEqual(golden.result);
    },
  );

  it("validates like GatheringController", () => {
    expect(validateGuideItems([])).toBe("At least one item is required.");
    expect(validateGuideItems([{ itemId: 0, quantity: 1 }])).toBe("Invalid item ID: 0.");
    expect(validateGuideItems([{ itemId: 5, quantity: 0 }])).toBe("Quantity must be at least 1 for item 5.");
    expect(validateGuideItems([{ itemId: 5, quantity: 1 }])).toBeNull();
  });
});

describe("Eorzean time", () => {
  const golden = readGoldenJson<EorzeanGolden>("eorzean-time.json");

  it("computes the hour like EorzeanTime.cs", () => {
    for (const { unixSeconds, hour } of golden.hours) expect(getEorzeanHour(unixSeconds * 1000)).toBe(hour);
  });

  it("evaluates windows like EorzeanTime.cs and formats like the mapper", () => {
    for (const w of golden.windows) {
      expect(isWindowActive(w.startHour, w.endHour, w.currentHour)).toBe(w.isActive);
      const seconds = timeUntilWindowSeconds(w.startHour, w.endHour, w.currentHour);
      expect(seconds).toBe(w.secondsUntil);
      expect(formatTimeUntil(seconds)).toBe(w.timeUntil);
    }
  });
});
