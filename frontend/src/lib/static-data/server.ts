import "server-only";
import { readdir, readFile } from "node:fs/promises";
import { join } from "node:path";
import type { CrystalTable } from "./materials";
import { CRYSTALS_PATH, DATA_BASE, DATA_FALLBACK_BASE, DATA_ROOT, DATA_VERSION, MANIFEST_PATH, recipeBucketPath } from "./paths";
import type { Bucket, Manifest, RecipeExport } from "./types";

// Server-side reads of the data set. At build time (sitemap, any prerender) it is on disk in the
// directory scripts/data-fetch.mjs placed it. A recipe page rendered on demand runs in a function
// without that directory, so it reads the same file over HTTP instead: from the CDN when the data
// set is served from one, else from the deployment's own static assets. The result is cached per
// deployment like any static route.

function dataDir(): string {
  return process.env.FFXIV_DATA_LOCAL_DIR || join(process.cwd(), "public", DATA_ROOT, DATA_VERSION);
}

const remoteBase = /^https?:\/\//.test(DATA_BASE) ? DATA_BASE : null;
// The CDN first; then the fallback origin for a file it refuses (see scripts/data-fetch.mjs). Both
// serve the same commit, so a 404 from the first is final.
const remoteBases = remoteBase ? [remoteBase, ...(DATA_FALLBACK_BASE ? [DATA_FALLBACK_BASE] : [])] : [];

// Vercel's deployment protection covers every generated deployment URL (VERCEL_URL), production
// ones included; only the production domain is public. So production reads through that domain,
// and previews through their own URL with the bypass secret.
function deploymentOrigin(): string | null {
  if (process.env.VERCEL_ENV === "production" && process.env.VERCEL_PROJECT_PRODUCTION_URL)
    return `https://${process.env.VERCEL_PROJECT_PRODUCTION_URL}`;
  if (process.env.VERCEL_URL) return `https://${process.env.VERCEL_URL}`;
  return process.env.NEXT_PUBLIC_SITE_URL ?? null;
}

async function fetchOverHttp<T>(relativePath: string): Promise<T | null> {
  if (DATA_VERSION === "") return null;
  if (remoteBase) {
    let lastError: unknown = null;
    for (const base of remoteBases) {
      try {
        const response = await fetch(`${base}/${relativePath}`, { cache: "force-cache" });
        if (response.status === 404) return null;
        if (response.ok) return (await response.json()) as T;
        lastError = new Error(`Game data ${relativePath} answered ${response.status} from ${base}`);
      } catch (err) {
        lastError = err;
      }
    }
    throw lastError;
  }
  const origin = deploymentOrigin();
  if (!origin) return null;
  const headers: Record<string, string> = {};
  // Preview deployments can sit behind Vercel's deployment protection.
  if (process.env.VERCEL_AUTOMATION_BYPASS_SECRET)
    headers["x-vercel-protection-bypass"] = process.env.VERCEL_AUTOMATION_BYPASS_SECRET;
  const response = await fetch(`${origin}${DATA_ROOT}/${DATA_VERSION}/${relativePath}`, {
    headers,
    cache: "force-cache",
  });
  if (response.status === 404) return null;
  if (!response.ok) throw new Error(`Game data ${relativePath} answered ${response.status}`);
  // A protected deployment answers with its login page after a redirect, not with the file.
  if (!(response.headers.get("content-type") ?? "").includes("json"))
    throw new Error(`Game data ${relativePath} from ${origin} is not JSON; is the deployment behind protection?`);
  return (await response.json()) as T;
}

const cache = new Map<string, Promise<unknown>>();

function readJson<T>(relativePath: string): Promise<T | null> {
  let promise = cache.get(relativePath);
  if (!promise) {
    promise = readFile(join(dataDir(), relativePath), "utf8")
      .then((text) => JSON.parse(text) as T)
      .catch((err: NodeJS.ErrnoException) => {
        if (err.code === "ENOENT") return fetchOverHttp<T>(relativePath);
        throw err;
      });
    cache.set(relativePath, promise);
    // A failed read is not kept: the next request tries again (the CDN may serve the file by then).
    promise.catch(() => cache.delete(relativePath));
  }
  return promise as Promise<T | null>;
}

export async function hasStaticData(): Promise<boolean> {
  return DATA_VERSION !== "" && (await readJson<Manifest>(MANIFEST_PATH)) !== null;
}

export function hasDataVersion(): boolean {
  return DATA_VERSION !== "";
}

export function getManifestServer(): Promise<Manifest | null> {
  return readJson<Manifest>(MANIFEST_PATH);
}

/** The crystal item table (ids 2–19 are not in the recipe buckets' material items). */
export async function getCrystalsServer(): Promise<CrystalTable> {
  return (await readJson<CrystalTable>(CRYSTALS_PATH)) ?? {};
}

export async function getRecipeExportServer(recipeId: number): Promise<RecipeExport | null> {
  if (!Number.isInteger(recipeId) || recipeId < 0) return null;
  const bucket = await readJson<Bucket<RecipeExport>>(recipeBucketPath(recipeId));
  return bucket?.[String(recipeId)] ?? null;
}

/** What the sitemap and the listing pages need about a recipe, without the recipe itself. */
export interface RecipeListEntry {
  recipeId: number;
  itemName: string;
  iconUrl: string;
  itemLevel: number;
  craftType: string;
  /** The recipe level (rlvl). */
  recipeLevel: number;
  /** The class job level the recipe unlocks at. */
  classJobLevel: number;
  stars: number;
}

let recipeListPromise: Promise<RecipeListEntry[]> | null = null;

/** Every recipe in the data set, ascending by id, read from the bucket files themselves. */
export function listRecipesServer(): Promise<RecipeListEntry[]> {
  recipeListPromise ??= readRecipeList();
  return recipeListPromise;
}

async function readRecipeList(): Promise<RecipeListEntry[]> {
  if (!(await hasStaticData())) return [];
  const dir = join(dataDir(), "recipes");
  let files: string[];
  try {
    files = await readdir(dir);
  } catch {
    return [];
  }
  const entries: RecipeListEntry[] = [];
  for (const file of files) {
    if (!file.endsWith(".json")) continue;
    const bucket = await readJson<Bucket<RecipeExport>>(`recipes/${file}`);
    if (!bucket) continue;
    for (const data of Object.values(bucket)) {
      entries.push({
        recipeId: data.recipe.recipeId,
        itemName: data.recipe.resultItem.name,
        iconUrl: data.recipe.resultItem.iconUrl,
        itemLevel: data.recipe.resultItem.itemLevel,
        craftType: data.recipe.craftType,
        recipeLevel: data.recipe.recipeLevel,
        classJobLevel: data.craftingParameters.classJobLevel,
        stars: data.craftingParameters.stars,
      });
    }
  }
  return entries.sort((a, b) => a.recipeId - b.recipeId);
}

/** Every recipe id in the data set, ascending. */
export async function listRecipeIdsServer(): Promise<number[]> {
  return (await listRecipesServer()).map((entry) => entry.recipeId);
}
