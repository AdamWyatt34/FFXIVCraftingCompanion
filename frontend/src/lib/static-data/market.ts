import type { ShoppingListItemDto } from "@/types/api";
import type { CostEstimateDto, MaterialCostDto, WorldGroupDto } from "@/types/market";
import { FFXIV_REGIONS } from "@/lib/server-data";

// Port of CostEstimationService + MarketMapper over the expanded tree's shopping list and
// crystals, with prices fetched from Universalis in the browser. Proven by
// goldens/cost-estimate.jsonl against a recorded Universalis response and a fixed clock.

export interface MarketBoardData {
  itemId: number;
  averagePriceNQ: number;
  averagePriceHQ: number;
  minPriceNQ: number;
  minPriceHQ: number;
  currentListingCount: number;
  /** Unix milliseconds. */
  lastUploadTime: number;
}

export interface CostEstimationOptions {
  useMinPrice?: boolean;
  includeCraftVsBuyAnalysis?: boolean;
  includeGatheredItemPrices?: boolean;
}

export const UNIVERSALIS_BASE_URL = "https://universalis.app/api/v2";
const MAX_BATCH_SIZE = 100;
const CHUNK_DELAY_MS = 250;
const FIELDS = ["itemID", "averagePriceNQ", "averagePriceHQ", "minPriceNQ", "minPriceHQ", "listingCount", "lastUploadTime"];

interface UniversalisItem {
  itemID?: number;
  averagePriceNQ?: number;
  averagePriceHQ?: number;
  minPriceNQ?: number;
  minPriceHQ?: number;
  listingCount?: number;
  lastUploadTime?: number;
}

/** UniversalisClient.MapToMarketBoardData for one item of a Universalis response. */
export function toMarketBoardData(item: UniversalisItem, itemId?: number): MarketBoardData {
  return {
    itemId: item.itemID ?? itemId ?? 0,
    averagePriceNQ: item.averagePriceNQ ?? 0,
    averagePriceHQ: item.averagePriceHQ ?? 0,
    minPriceNQ: item.minPriceNQ ?? 0,
    minPriceHQ: item.minPriceHQ ?? 0,
    currentListingCount: item.listingCount ?? 0,
    lastUploadTime: item.lastUploadTime ?? 0,
  };
}

/** The items of a Universalis batch response (`{ items: { "<id>": {...} } }`). */
export function parseUniversalisBatch(body: { items?: Record<string, UniversalisItem> }): Map<number, MarketBoardData> {
  const result = new Map<number, MarketBoardData>();
  for (const [key, item] of Object.entries(body.items ?? {})) {
    const id = Number(key);
    if (Number.isInteger(id)) result.set(id, toMarketBoardData(item, id));
  }
  return result;
}

/** UniversalisClient.GetItemPricesBatchAsync: chunks of 100, 250 ms apart, 404 chunks skipped. */
export async function fetchUniversalisPrices(
  worldOrDc: string,
  itemIds: number[],
  signal?: AbortSignal,
): Promise<Map<number, MarketBoardData>> {
  const result = new Map<number, MarketBoardData>();
  const ids = [...new Set(itemIds)];
  for (let i = 0; i < ids.length; i += MAX_BATCH_SIZE) {
    if (i > 0) await new Promise((resolve) => setTimeout(resolve, CHUNK_DELAY_MS));
    const chunk = ids.slice(i, i + MAX_BATCH_SIZE);
    const single = chunk.length === 1;
    const fields = (single ? FIELDS : FIELDS.map((f) => `items.${f}`)).join(",");
    const response = await fetch(
      `${UNIVERSALIS_BASE_URL}/${encodeURIComponent(worldOrDc)}/${chunk.join(",")}?fields=${fields}`,
      { signal, headers: { Accept: "application/json" } },
    );
    if (response.status === 404) continue;
    if (!response.ok) throw new Error(`Universalis answered ${response.status}`);
    if (single) {
      const item = (await response.json()) as UniversalisItem;
      const data = toMarketBoardData(item, chunk[0]);
      result.set(data.itemId, data);
    } else {
      for (const [id, data] of parseUniversalisBatch((await response.json()) as { items?: Record<string, UniversalisItem> }))
        result.set(id, data);
    }
  }
  return result;
}

const NON_GIL_SOURCES = new Set(["Scrip Exchange", "Quest Reward", "Dungeon Drop", "Mob Drop", "Desynthesis", "Aetherial Reduction"]);

function vendorCost(mat: ShoppingListItemDto): number | null {
  if (mat.primarySource !== "Vendor") return null;
  const source = mat.sources.find((s) => s.type === "Vendor" && s.cost !== null && s.cost !== undefined);
  return source ? source.cost! : null;
}

function needsMarketBoardPrice(mat: ShoppingListItemDto, options: Required<CostEstimationOptions>): boolean {
  if (vendorCost(mat) !== null) return false;
  if (NON_GIL_SOURCES.has(mat.primarySource)) return false;
  if (mat.primarySource === "Gathered") return options.includeGatheredItemPrices;
  return true;
}

// (long)decimal truncates the exact product; the epsilon keeps a product that is exactly an
// integer in decimal from landing a hair below it in binary.
function truncate(value: number): number {
  return Math.trunc(value + 1e-9);
}

/** System.Text.Json's DateTime (UTC) format: fraction trimmed of trailing zeros, no fraction when zero. */
export function toApiDateTime(ms: number): string {
  const iso = new Date(ms).toISOString();
  const fraction = iso.slice(19, 23).replace(/0+$/, "");
  return iso.slice(0, 19) + (fraction === "." ? "" : fraction) + "Z";
}

export function estimateCost(
  materials: ShoppingListItemDto[],
  finalItemId: number | null,
  worldOrDc: string,
  opts: CostEstimationOptions,
  prices: Map<number, MarketBoardData>,
  nowMs: number,
  warnings: string[] = [],
): CostEstimateDto {
  const options: Required<CostEstimationOptions> = {
    useMinPrice: opts.useMinPrice ?? false,
    includeCraftVsBuyAnalysis: opts.includeCraftVsBuyAnalysis ?? true,
    includeGatheredItemPrices: opts.includeGatheredItemPrices ?? true,
  };
  const allWarnings = [...warnings];
  const materialCosts: MaterialCostDto[] = [];
  let totalVendorCost = 0;
  let totalMarketBoardCost = 0;

  for (const mat of materials) {
    const cost = materialCost(mat, prices, options, nowMs);
    materialCosts.push(cost);
    if (cost.priceSource === "Vendor") totalVendorCost += cost.totalPrice;
    else if (cost.priceSource === "MarketBoard") totalMarketBoardCost += cost.totalPrice;
  }
  const totalEstimatedCost = totalVendorCost + totalMarketBoardCost;

  const unknownCount = materialCosts.filter((m) => m.priceSource === "Unknown").length;
  if (unknownCount > 0)
    allWarnings.push(`${unknownCount} material(s) have no price data — cost estimate may be incomplete.`);

  const pricedCount = materialCosts.filter((m) => m.priceSource === "Vendor" || m.priceSource === "MarketBoard").length;
  const hasMeaningfulCosts = pricedCount > 0 && unknownCount <= pricedCount;

  let finalItemMarketPrice: number | null = null;
  let craftingIsCheaper: boolean | null = null;
  let savings: number | null = null;
  const finalData = finalItemId !== null && options.includeCraftVsBuyAnalysis ? prices.get(finalItemId) : undefined;
  if (finalData) {
    const finalPrice = options.useMinPrice ? finalData.minPriceNQ : finalData.averagePriceNQ;
    if (finalPrice > 0) {
      finalItemMarketPrice = finalPrice;
      if (hasMeaningfulCosts) {
        craftingIsCheaper = totalEstimatedCost < truncate(finalPrice);
        savings = truncate(finalPrice) - totalEstimatedCost;
      } else {
        allWarnings.push("Craft vs. buy comparison unavailable — too many materials lack price data.");
      }
    }
  }

  return {
    worldOrDc,
    generatedAt: toApiDateTime(nowMs),
    materialCosts,
    totalVendorCost,
    totalMarketBoardCost,
    totalEstimatedCost,
    finalItemMarketPrice,
    craftingIsCheaper,
    savings,
    warnings: allWarnings,
  };
}

function materialCost(
  mat: ShoppingListItemDto,
  prices: Map<number, MarketBoardData>,
  options: Required<CostEstimationOptions>,
  nowMs: number,
): MaterialCostDto {
  const base = { itemId: mat.itemId, itemName: mat.itemName, quantity: mat.totalQuantity };
  const vendor = vendorCost(mat);
  if (vendor !== null) {
    return { ...base, unitPrice: vendor, totalPrice: vendor * mat.totalQuantity, priceSource: "Vendor", isStalePrice: false, listingCount: 0, lastUploadTime: null };
  }
  if (NON_GIL_SOURCES.has(mat.primarySource)) {
    return { ...base, unitPrice: 0, totalPrice: 0, priceSource: "NonGil", isStalePrice: false, listingCount: 0, lastUploadTime: null };
  }
  const data = prices.get(mat.itemId);
  if (data) {
    const unitPrice = options.useMinPrice ? data.minPriceNQ : data.averagePriceNQ;
    return {
      ...base,
      unitPrice,
      totalPrice: truncate(unitPrice * mat.totalQuantity),
      priceSource: "MarketBoard",
      isStalePrice: (nowMs - data.lastUploadTime) / 3_600_000 > 24,
      listingCount: data.currentListingCount,
      lastUploadTime: toApiDateTime(data.lastUploadTime),
    };
  }
  return { ...base, unitPrice: 0, totalPrice: 0, priceSource: "Unknown", isStalePrice: false, listingCount: 0, lastUploadTime: null };
}

/** The item ids a cost estimate asks Universalis for (CostEstimationService's selection). */
export function marketBoardItemIds(
  materials: ShoppingListItemDto[],
  finalItemId: number | null,
  opts: CostEstimationOptions,
): number[] {
  const options: Required<CostEstimationOptions> = {
    useMinPrice: opts.useMinPrice ?? false,
    includeCraftVsBuyAnalysis: opts.includeCraftVsBuyAnalysis ?? true,
    includeGatheredItemPrices: opts.includeGatheredItemPrices ?? true,
  };
  const ids = new Set<number>();
  for (const mat of materials) if (needsMarketBoardPrice(mat, options)) ids.add(mat.itemId);
  if (finalItemId !== null && options.includeCraftVsBuyAnalysis) ids.add(finalItemId);
  return [...ids];
}

/** GET /api/market/worlds from the same server list the API holds. */
export function worldGroups(): WorldGroupDto[] {
  return FFXIV_REGIONS.map((r) => ({
    region: r.region,
    dataCenters: r.dataCenters.map((dc) => ({ name: dc.name, worlds: [...dc.servers] })),
  }));
}

export function isValidWorldOrDc(name: string): boolean {
  const wanted = name.toUpperCase();
  return FFXIV_REGIONS.some((r) =>
    r.dataCenters.some((dc) => dc.name.toUpperCase() === wanted || dc.servers.some((s) => s.toUpperCase() === wanted)),
  );
}
