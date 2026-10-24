/**
 * Format: action IDs packed as Uint8Array (IDs 1-253), then base64url encoded.
 * Wider IDs carry a prefix byte (0xFF Uint16BE, 0xFE 3-byte BE); the narrow form stops below both so its first byte is never read as one.
 */

import { SIMULATOR_CONSTANTS } from "@/lib/constants";
import { isValidCrafterStats } from "@/lib/stat-limits";
import type { ConsumableChoice, ConsumableSelection } from "@/types/simulator";

const PREFIX_WIDE = 0xff;
const PREFIX_WIDE24 = 0xfe;

/** A shared rotation never carries more steps than a craft can. */
export const MAX_SHARED_ACTIONS = SIMULATOR_CONSTANTS.MAX_ACTIONS_PER_CRAFT;
/** The widest id the 3-byte form carries; every shared-rotation reader bounds ids by it. */
export const MAX_ACTION_ID = 0xffffff;

function boundedIds(ids: number[]): number[] | null {
  if (ids.length === 0 || ids.length > MAX_SHARED_ACTIONS) return null;
  return ids.every((id) => Number.isInteger(id) && id >= 1 && id <= MAX_ACTION_ID) ? ids : null;
}

/** Encode an array of action IDs into a compact base64url string. */
export function encodeRotation(actionIds: number[]): string {
  if (actionIds.length === 0) return "";

  const highest = Math.max(...actionIds);
  const width = highest > 0xffff ? 3 : highest >= PREFIX_WIDE24 ? 2 : 1;

  let bytes: Uint8Array;
  if (width === 1) {
    bytes = new Uint8Array(actionIds);
  } else {
    bytes = new Uint8Array(1 + actionIds.length * width);
    bytes[0] = width === 3 ? PREFIX_WIDE24 : PREFIX_WIDE;
    actionIds.forEach((id, i) => {
      for (let b = 0; b < width; b++) {
        bytes[1 + i * width + b] = (id >> (8 * (width - 1 - b))) & 0xff;
      }
    });
  }

  const base64 = btoa(String.fromCharCode(...bytes));
  return base64.replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
}

/** Null on invalid input, an empty rotation, a zero id in a wide form, a wide payload that is not a whole number of ids, or more steps than a craft holds. */
export function decodeRotation(encoded: string): number[] | null {
  if (!encoded) return null;

  try {
    const base64 = encoded.replace(/-/g, "+").replace(/_/g, "/");
    const padded = base64 + "=".repeat((4 - (base64.length % 4)) % 4);
    const binary = atob(padded);
    const bytes = new Uint8Array(binary.length);
    for (let i = 0; i < binary.length; i++) {
      bytes[i] = binary.charCodeAt(i);
    }

    if (bytes.length === 0) return null;

    const width = bytes[0] === PREFIX_WIDE24 ? 3 : bytes[0] === PREFIX_WIDE ? 2 : 1;
    if (width === 1) return boundedIds(Array.from(bytes).filter((b) => b > 0));

    const count = (bytes.length - 1) / width;
    if (!Number.isInteger(count)) return null;
    const ids: number[] = [];
    for (let i = 0; i < count; i++) {
      let id = 0;
      for (let b = 0; b < width; b++) id = (id << 8) | bytes[1 + i * width + b];
      ids.push(id);
    }
    return boundedIds(ids);
  } catch {
    return null;
  }
}

/** What a shared craft starts from besides the four base stats. */
export interface ShareContext {
  consumables: ConsumableSelection;
  isSpecialist: boolean;
  startingQuality: number;
}

const MAX_INT32 = 2147483647;

export function encodeShareContext(search: URLSearchParams, ctx: ShareContext): void {
  const slots = [
    ["meal", ctx.consumables.meal],
    ["medicine", ctx.consumables.medicine],
  ] as const;
  for (const [name, choice] of slots) {
    if (!choice) continue;
    search.set(name, String(choice.itemId));
    if (choice.hq) search.set(`${name}Hq`, "1");
  }
  if (ctx.isSpecialist) search.set("sp", "1");
  if (ctx.startingQuality > 0) search.set("sq", String(Math.trunc(ctx.startingQuality)));
}

/** Tolerant reader: an unusable item id empties its slot, only "1" sets a flag, and a non-integer starting quality reads as 0. The simulator clamps the quality to the recipe's. */
export function decodeShareContext(search: URLSearchParams): ShareContext {
  return {
    consumables: {
      meal: decodeChoice(search, "meal"),
      medicine: decodeChoice(search, "medicine"),
    },
    isSpecialist: search.get("sp") === "1",
    startingQuality: decodeStartingQuality(search.get("sq")),
  };
}

function decodeChoice(search: URLSearchParams, name: string): ConsumableChoice | null {
  const raw = search.get(name);
  if (raw === null || !/^\d+$/.test(raw)) return null;
  const itemId = Number(raw);
  if (itemId < 1 || itemId > MAX_INT32) return null;
  return { itemId, hq: search.get(`${name}Hq`) === "1" };
}

function decodeStartingQuality(raw: string | null): number {
  if (raw === null || !/^-?\d+$/.test(raw)) return 0;
  return Math.min(MAX_INT32, Math.max(0, Number(raw)));
}

/** `stats` are the base stats; the consumables travel in `context`. */
export function buildSolverShareUrl(params: {
  recipeId: number;
  stats: { craftsmanship: number; control: number; cp: number; level: number };
  actionIds: number[];
  algorithm?: string;
  context?: ShareContext;
}): string {
  const base = typeof window !== "undefined" ? window.location.origin : "";
  const search = new URLSearchParams({
    recipeId: String(params.recipeId),
    c: String(params.stats.craftsmanship),
    ct: String(params.stats.control),
    cp: String(params.stats.cp),
    lv: String(params.stats.level),
    r: encodeRotation(params.actionIds),
  });
  if (params.algorithm) {
    search.set("alg", params.algorithm);
  }
  if (params.context) encodeShareContext(search, params.context);
  return `${base}/simulator?solve=1&${search.toString()}`;
}

/** Parse a solver share URL's search params back into rotation data. Returns null if not a solver URL. */
export function parseSolverShareUrl(
  searchParams: URLSearchParams,
): {
  recipeId: number;
  stats: { craftsmanship: number; control: number; cp: number; level: number };
  actionIds: number[];
  algorithm?: string;
  context: ShareContext;
} | null {
  if (searchParams.get("solve") !== "1") return null;

  const recipeId = Number(searchParams.get("recipeId"));
  const craftsmanship = Number(searchParams.get("c"));
  const control = Number(searchParams.get("ct"));
  const cp = Number(searchParams.get("cp"));
  const level = Number(searchParams.get("lv"));
  const rotation = searchParams.get("r");
  const algorithm = searchParams.get("alg") ?? undefined;

  if (!Number.isSafeInteger(recipeId) || recipeId < 1) return null;

  const stats = { craftsmanship, control, cp, level };
  if (!isValidCrafterStats(stats)) return null;

  const actionIds = decodeRotation(rotation ?? "");
  if (!actionIds) return null;

  return {
    recipeId,
    stats,
    actionIds,
    algorithm: algorithm && /^[a-z]{1,16}$/.test(algorithm) ? algorithm : undefined,
    context: decodeShareContext(searchParams),
  };
}
