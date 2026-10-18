// Where the exported game data lives. The version is baked in at build time from data.lock.json
// (or the local data set fed to scripts/data-fetch.mjs), so one deployment references exactly one
// data version and the service worker can cache /data/v/* immutably.

export const DATA_VERSION = process.env.NEXT_PUBLIC_DATA_VERSION ?? "";
export const DATA_BUCKET_SIZE = Number(process.env.NEXT_PUBLIC_DATA_BUCKET_SIZE ?? 100);

export const DATA_ROOT = "/data/v";

export function dataBasePath(): string {
  return `${DATA_ROOT}/${DATA_VERSION}`;
}

export function bucketOf(id: number, bucketSize = DATA_BUCKET_SIZE): number {
  return Math.floor(id / bucketSize);
}

export function recipeBucketPath(recipeId: number): string {
  return `recipes/${bucketOf(recipeId)}.json`;
}

export function itemBucketPath(itemId: number): string {
  return `items/${bucketOf(itemId)}.json`;
}

export const SEARCH_INDEX_PATH = "search/items.json";
/** The same index as interned tables and columns; absent from data sets exported before it existed. */
export const SEARCH_COMPACT_PATH = "search/compact.json";
export const CRYSTALS_PATH = "materials/crystals.json";
export const MANIFEST_PATH = "manifest.json";

export const GEAR_PROFILES_PATH = "gear/profiles.json";

export function gearJobPath(job: string): string {
  return `gear/${job.toUpperCase()}.json`;
}
