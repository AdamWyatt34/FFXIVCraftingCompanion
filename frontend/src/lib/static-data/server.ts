import "server-only";
import { readdir, readFile } from "node:fs/promises";
import { join } from "node:path";
import { DATA_ROOT, DATA_VERSION, MANIFEST_PATH, recipeBucketPath } from "./paths";
import type { Bucket, Manifest, RecipeExport } from "./types";

// Build-time reads for static generation. scripts/data-fetch.mjs puts the data set under
// public/data/v/<version>/ before `next build`; without it there is nothing to prerender.

function dataDir(): string {
  return join(process.cwd(), "public", DATA_ROOT, DATA_VERSION);
}

const cache = new Map<string, Promise<unknown>>();

function readJson<T>(relativePath: string): Promise<T | null> {
  let promise = cache.get(relativePath);
  if (!promise) {
    promise = readFile(join(dataDir(), relativePath), "utf8")
      .then((text) => JSON.parse(text) as T)
      .catch((err: NodeJS.ErrnoException) => {
        if (err.code === "ENOENT") return null;
        throw err;
      });
    cache.set(relativePath, promise);
  }
  return promise as Promise<T | null>;
}

export async function hasStaticData(): Promise<boolean> {
  return DATA_VERSION !== "" && (await readJson<Manifest>(MANIFEST_PATH)) !== null;
}

export function getManifestServer(): Promise<Manifest | null> {
  return readJson<Manifest>(MANIFEST_PATH);
}

export async function getRecipeExportServer(recipeId: number): Promise<RecipeExport | null> {
  if (!Number.isInteger(recipeId) || recipeId < 0) return null;
  const bucket = await readJson<Bucket<RecipeExport>>(recipeBucketPath(recipeId));
  return bucket?.[String(recipeId)] ?? null;
}

/** Every recipe id in the data set, ascending, read from the bucket files themselves. */
export async function listRecipeIdsServer(): Promise<number[]> {
  if (!(await hasStaticData())) return [];
  const dir = join(dataDir(), "recipes");
  let files: string[];
  try {
    files = await readdir(dir);
  } catch {
    return [];
  }
  const ids: number[] = [];
  for (const file of files) {
    if (!file.endsWith(".json")) continue;
    const bucket = await readJson<Bucket<unknown>>(`recipes/${file}`);
    if (bucket) ids.push(...Object.keys(bucket).map(Number));
  }
  return ids.sort((a, b) => a - b);
}
