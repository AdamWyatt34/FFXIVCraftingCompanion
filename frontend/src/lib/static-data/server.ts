import "server-only";
import { readdir, readFile } from "node:fs/promises";
import { join } from "node:path";
import { DATA_ROOT, DATA_VERSION, MANIFEST_PATH, recipeBucketPath } from "./paths";
import type { Bucket, Manifest, RecipeExport } from "./types";

// Server-side reads of the data set. At build time (sitemap, any prerender) it is on disk under
// public/data/v/<version>/, placed by scripts/data-fetch.mjs. A recipe page rendered on demand
// runs in a function without that directory, so it reads the same file from the deployment's own
// static assets instead; the result is cached per deployment like any static route.

function dataDir(): string {
  return join(process.cwd(), "public", DATA_ROOT, DATA_VERSION);
}

// Vercel's deployment protection covers every generated deployment URL (VERCEL_URL), production
// ones included; only the production domain is public. So production reads through that domain,
// and previews through their own URL with the bypass secret.
function deploymentOrigin(): string | null {
  if (process.env.VERCEL_ENV === "production" && process.env.VERCEL_PROJECT_PRODUCTION_URL)
    return `https://${process.env.VERCEL_PROJECT_PRODUCTION_URL}`;
  if (process.env.VERCEL_URL) return `https://${process.env.VERCEL_URL}`;
  return process.env.NEXT_PUBLIC_SITE_URL ?? null;
}

async function fetchFromDeployment<T>(relativePath: string): Promise<T | null> {
  const origin = deploymentOrigin();
  if (!origin || DATA_VERSION === "") return null;
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
        if (err.code === "ENOENT") return fetchFromDeployment<T>(relativePath);
        throw err;
      });
    cache.set(relativePath, promise);
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
