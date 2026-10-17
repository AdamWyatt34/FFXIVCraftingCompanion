import type {
  CrystalRequirementDto,
  EorzeanTimeWindowDto,
  GatheringGuideDto,
  GatheringGuideItemDto,
  GatheringGuideItemInput,
  TimedNodeEntryDto,
} from "@/types/gathering";
import type { ItemDto } from "@/types/api";
import type { GatheringNodeExport } from "./types";
import { compareOrdinalIgnoreCase } from "./materials";
import { isWindowActive, timeUntilWindowSeconds } from "@/lib/eorzean-time";

// Port of GatheringGuideService + GatheringGuideMapper. The caller supplies the item lookup
// (name, icon, gathering rows from the item export) and the Eorzean hour to render against.
// Proven by goldens/gathering-guide.jsonl and goldens/eorzean-time.json.

export interface GatheringItemData {
  item: ItemDto;
  gatheringNodes: GatheringNodeExport[];
}

export type GatheringItemLookup = (itemId: number) => GatheringItemData | null;

const CRYSTAL_MAP: Record<number, { element: string; tier: string }> = {
  2: { element: "Fire", tier: "Shard" }, 3: { element: "Ice", tier: "Shard" }, 4: { element: "Wind", tier: "Shard" },
  5: { element: "Earth", tier: "Shard" }, 6: { element: "Lightning", tier: "Shard" }, 7: { element: "Water", tier: "Shard" },
  8: { element: "Fire", tier: "Crystal" }, 9: { element: "Ice", tier: "Crystal" }, 10: { element: "Wind", tier: "Crystal" },
  11: { element: "Earth", tier: "Crystal" }, 12: { element: "Lightning", tier: "Crystal" }, 13: { element: "Water", tier: "Crystal" },
  14: { element: "Fire", tier: "Cluster" }, 15: { element: "Ice", tier: "Cluster" }, 16: { element: "Wind", tier: "Cluster" },
  17: { element: "Earth", tier: "Cluster" }, 18: { element: "Lightning", tier: "Cluster" }, 19: { element: "Water", tier: "Cluster" },
};

export const MAX_GUIDE_ITEMS = 200;

/** GatheringController's request validation: the 400 message, or null. */
export function validateGuideItems(items: GatheringGuideItemInput[] | null | undefined): string | null {
  if (!items || items.length === 0) return "At least one item is required.";
  if (items.length > MAX_GUIDE_ITEMS) return `Maximum of ${MAX_GUIDE_ITEMS} items per guide request.`;
  for (const item of items) {
    if (item.itemId <= 0) return `Invalid item ID: ${item.itemId}.`;
    if (item.quantity < 1) return `Quantity must be at least 1 for item ${item.itemId}.`;
  }
  return null;
}

const isCrystalId = (id: number) => id >= 2 && id <= 19;

function parseHour(timeString: string): number {
  const part = timeString.split(":")[0];
  return /^\s*[+-]?\d+\s*$/.test(part) ? Number(part) : -1;
}

function parseTimeWindow(spawnTimes: string | null): { startHour: number; endHour: number } | null {
  if (!spawnTimes) return null;
  try {
    const times = JSON.parse(spawnTimes) as unknown;
    if (!Array.isArray(times) || times.length < 2) return null;
    const startHour = parseHour(String(times[0]));
    const endHour = parseHour(String(times[1]));
    if (startHour < 0 || endHour < 0) return null;
    return { startHour, endHour };
  } catch {
    return null;
  }
}

function pickPreferredNode(nodes: GatheringNodeExport[]): GatheringNodeExport {
  return [...nodes].sort(
    (a, b) =>
      (a.nodeType !== "Standard" ? 1 : 0) - (b.nodeType !== "Standard" ? 1 : 0) ||
      (a.isHidden ? 1 : 0) - (b.isHidden ? 1 : 0) ||
      a.level - b.level,
  )[0];
}

/** GatheringGuideMapper.FormatTimeUntil over whole seconds. */
export function formatTimeUntil(totalSeconds: number): string {
  if (totalSeconds / 60 < 1) return "< 1 min";
  const minutes = Math.floor(totalSeconds / 60) % 60;
  const seconds = Math.floor(totalSeconds) % 60;
  return totalSeconds / 3600 >= 1 ? `${Math.floor(totalSeconds / 3600)}h ${minutes}m` : `${minutes}m ${seconds}s`;
}

function mapWindow(startHour: number, endHour: number, currentHour: number): EorzeanTimeWindowDto {
  const isActive = isWindowActive(startHour, endHour, currentHour);
  return {
    startHour,
    endHour,
    isCurrentlyActive: isActive,
    timeUntil: isActive ? null : formatTimeUntil(timeUntilWindowSeconds(startHour, endHour, currentHour)),
  };
}

const emptyGuide = (): GatheringGuideDto => ({
  zoneGroups: [],
  timedNodes: [],
  crystalSummary: [],
  summary: { totalZones: 0, totalItems: 0, timedNodeCount: 0, totalCrystalTypes: 0 },
});

export function buildGatheringGuide(
  materials: GatheringGuideItemInput[],
  lookup: GatheringItemLookup,
  currentHour: number,
): GatheringGuideDto {
  if (materials.length === 0) return emptyGuide();

  const nameOf = (id: number) => lookup(id)?.item.name ?? `Item ${id}`;
  const iconOf = (id: number) => lookup(id)?.item.iconUrl ?? "";

  const crystalSummary: CrystalRequirementDto[] = materials
    .filter((m) => isCrystalId(m.itemId) && m.itemId in CRYSTAL_MAP)
    .map((m) => ({
      itemId: m.itemId,
      itemName: nameOf(m.itemId),
      iconUrl: iconOf(m.itemId),
      quantity: m.quantity,
      element: CRYSTAL_MAP[m.itemId].element,
      tier: CRYSTAL_MAP[m.itemId].tier,
    }))
    .sort((a, b) => compareOrdinalIgnoreCase(a.tier, b.tier) || compareOrdinalIgnoreCase(a.element, b.element));

  const guideItems: { item: GatheringGuideItemDto; zoneName: string }[] = [];
  const timedNodes: TimedNodeEntryDto[] = [];

  for (const input of materials.filter((m) => !isCrystalId(m.itemId))) {
    const nodes = lookup(input.itemId)?.gatheringNodes ?? [];
    if (nodes.length === 0) continue;

    const preferred = pickPreferredNode(nodes);
    const window = parseTimeWindow(preferred.spawnTimes);
    const itemName = nameOf(input.itemId);
    const iconUrl = iconOf(input.itemId);

    guideItems.push({
      zoneName: preferred.zoneName,
      item: {
        itemId: input.itemId,
        itemName,
        iconUrl,
        quantity: input.quantity,
        gatheringClass: preferred.classJob,
        gatheringLevel: preferred.level,
        nodeType: preferred.nodeType,
        coordinateX: preferred.coordinateX,
        coordinateY: preferred.coordinateY,
        timeWindow: window ? mapWindow(window.startHour, window.endHour, currentHour) : null,
        isHidden: preferred.isHidden,
      },
    });

    if (preferred.isTimed && window) {
      const mapped = mapWindow(window.startHour, window.endHour, currentHour);
      timedNodes.push({
        itemId: input.itemId,
        itemName,
        iconUrl,
        quantity: input.quantity,
        zoneName: preferred.zoneName,
        gatheringClass: preferred.classJob,
        gatheringLevel: preferred.level,
        nodeType: preferred.nodeType,
        startHour: window.startHour,
        endHour: window.endHour,
        isCurrentlyActive: mapped.isCurrentlyActive,
        timeUntil: mapped.timeUntil,
        coordinateX: preferred.coordinateX,
        coordinateY: preferred.coordinateY,
      });
    }
  }

  const zones = new Map<string, GatheringGuideItemDto[]>();
  for (const { zoneName, item } of guideItems) {
    const list = zones.get(zoneName);
    if (list) list.push(item);
    else zones.set(zoneName, [item]);
  }
  const zoneGroups = [...zones.entries()]
    .sort(([a], [b]) => compareOrdinalIgnoreCase(a, b))
    .map(([zoneName, items]) => ({
      zoneName,
      items: [...items].sort(
        (a, b) => compareOrdinalIgnoreCase(a.gatheringClass, b.gatheringClass) || a.gatheringLevel - b.gatheringLevel,
      ),
    }));

  const sortedTimed = [...timedNodes].sort((a, b) => a.startHour - b.startHour);

  return {
    zoneGroups,
    timedNodes: sortedTimed,
    crystalSummary,
    summary: {
      totalZones: zoneGroups.length,
      totalItems: guideItems.length,
      timedNodeCount: sortedTimed.length,
      totalCrystalTypes: crystalSummary.length,
    },
  };
}
