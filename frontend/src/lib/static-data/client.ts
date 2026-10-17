import { ApiClientError } from "@/lib/api-errors";
import type { ItemDto, ItemSearchResultDto, RecipeCraftingParametersDto, RecipeDto } from "@/types/api";
import type { ItemContextDto } from "@/types/journey";
import { dataBasePath, itemBucketPath, recipeBucketPath, SEARCH_INDEX_PATH } from "./paths";
import { searchIndex } from "./search";
import { toRecipeDto } from "./recipes";
import type { Bucket, ItemExport, RecipeExport, SearchIndexEntry } from "./types";

// Browser-side reads of the exported game data. Buckets are fetched once per page load and kept;
// the service worker keeps them across loads.

const inflight = new Map<string, Promise<unknown>>();

export function loadJson<T>(relativePath: string, signal?: AbortSignal): Promise<T> {
  const existing = inflight.get(relativePath);
  if (existing) return existing as Promise<T>;

  const promise = fetchJson<T>(relativePath, signal).catch((err) => {
    inflight.delete(relativePath);
    throw err;
  });
  inflight.set(relativePath, promise);
  return promise;
}

async function fetchJson<T>(relativePath: string, signal?: AbortSignal): Promise<T> {
  const response = await fetch(`${dataBasePath()}/${relativePath}`, { signal, headers: { Accept: "application/json" } });
  if (!response.ok) {
    throw new ApiClientError(response.status, {
      error: response.status === 404 ? "Not found" : `Game data request failed (${response.status})`,
    });
  }
  return (await response.json()) as T;
}

function notFound(what: string): ApiClientError {
  return new ApiClientError(404, { error: `${what} not found` });
}

async function fromBucket<T>(path: string, id: number, what: string, signal?: AbortSignal): Promise<T> {
  let bucket: Bucket<T>;
  try {
    bucket = await loadJson<Bucket<T>>(path, signal);
  } catch (err) {
    if (err instanceof ApiClientError && err.status === 404) throw notFound(what);
    throw err;
  }
  const entry = bucket[String(id)];
  if (!entry) throw notFound(what);
  return entry;
}

export function getRecipeExport(recipeId: number, signal?: AbortSignal): Promise<RecipeExport> {
  return fromBucket<RecipeExport>(recipeBucketPath(recipeId), recipeId, "Recipe", signal);
}

export function getItemExport(itemId: number, signal?: AbortSignal): Promise<ItemExport> {
  return fromBucket<ItemExport>(itemBucketPath(itemId), itemId, "Item", signal);
}

export async function getRecipe(recipeId: number, includeMaterials = false): Promise<RecipeDto> {
  return toRecipeDto(await getRecipeExport(recipeId), includeMaterials);
}

export async function getRecipeCraftingParameters(
  recipeId: number,
  signal?: AbortSignal,
): Promise<RecipeCraftingParametersDto> {
  return (await getRecipeExport(recipeId, signal)).craftingParameters;
}

export async function getItem(itemId: number): Promise<ItemDto> {
  return (await getItemExport(itemId)).item;
}

export async function getItemContext(itemId: number, signal?: AbortSignal): Promise<ItemContextDto> {
  return (await getItemExport(itemId, signal)).context;
}

export function getSearchIndex(signal?: AbortSignal): Promise<SearchIndexEntry[]> {
  return loadJson<SearchIndexEntry[]>(SEARCH_INDEX_PATH, signal);
}

export async function searchItems(
  query: string | null,
  page = 1,
  pageSize = 20,
  signal?: AbortSignal,
  craftType?: string,
): Promise<ItemSearchResultDto> {
  const index = await getSearchIndex(signal);
  if (signal?.aborted) throw new DOMException("Aborted", "AbortError");
  return searchIndex(index, query, page, pageSize, craftType ?? null);
}
