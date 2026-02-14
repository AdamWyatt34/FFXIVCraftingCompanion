/**
 * Compact rotation encoding for URL sharing.
 *
 * Format: action IDs packed as Uint8Array (IDs 1-255), then base64url encoded.
 * If any ID > 255, falls back to Uint16BE with a 0xFF prefix byte marker.
 *
 * For typical 25-step rotations:
 *   base64url: ~34 chars vs comma-separated: ~75 chars
 */

const PREFIX_WIDE = 0xff;

/** Encode an array of action IDs into a compact base64url string. */
export function encodeRotation(actionIds: number[]): string {
  if (actionIds.length === 0) return "";

  const needsWide = actionIds.some((id) => id > 255);

  let bytes: Uint8Array;
  if (needsWide) {
    bytes = new Uint8Array(1 + actionIds.length * 2);
    bytes[0] = PREFIX_WIDE;
    for (let i = 0; i < actionIds.length; i++) {
      bytes[1 + i * 2] = (actionIds[i] >> 8) & 0xff;
      bytes[1 + i * 2 + 1] = actionIds[i] & 0xff;
    }
  } else {
    bytes = new Uint8Array(actionIds);
  }

  const base64 = btoa(String.fromCharCode(...bytes));
  return base64.replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
}

/** Decode a base64url rotation string back into action IDs. Returns null on invalid input. */
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

    if (bytes[0] === PREFIX_WIDE) {
      const count = (bytes.length - 1) / 2;
      if (count !== Math.floor(count)) return null;
      const ids: number[] = [];
      for (let i = 0; i < count; i++) {
        ids.push((bytes[1 + i * 2] << 8) | bytes[1 + i * 2 + 1]);
      }
      return ids.length > 0 ? ids : null;
    }

    const ids = Array.from(bytes).filter((b) => b > 0);
    return ids.length > 0 ? ids : null;
  } catch {
    return null;
  }
}

/** Build a solver share URL with compact encoded rotation. */
export function buildSolverShareUrl(params: {
  recipeId: number;
  stats: { craftsmanship: number; control: number; cp: number; level: number };
  actionIds: number[];
  algorithm?: string;
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
} | null {
  if (searchParams.get("solve") !== "1") return null;

  const recipeId = Number(searchParams.get("recipeId"));
  const craftsmanship = Number(searchParams.get("c"));
  const control = Number(searchParams.get("ct"));
  const cp = Number(searchParams.get("cp"));
  const level = Number(searchParams.get("lv"));
  const rotation = searchParams.get("r");
  const algorithm = searchParams.get("alg") ?? undefined;

  if (!recipeId || !craftsmanship || !cp || !level) return null;

  const actionIds = decodeRotation(rotation ?? "");
  if (!actionIds) return null;

  return {
    recipeId,
    stats: { craftsmanship, control, cp, level },
    actionIds,
    algorithm,
  };
}
