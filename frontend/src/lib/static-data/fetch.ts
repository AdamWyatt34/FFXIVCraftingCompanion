import { ApiClientError } from "@/lib/api-errors";
import { dataBases } from "./paths";

// Browser-side reads of the exported game data. Files are fetched once per page load and kept;
// the service worker keeps them across loads. Every base serves the same commit of the data set,
// so a file missing from one is missing from all and a 404 is final, while any other failure,
// such as the CDN refusing a file it has not stored yet, moves on to the next base.

const inflight = new Map<string, Promise<unknown>>();

export function loadJson<T>(relativePath: string, signal?: AbortSignal): Promise<T> {
  const existing = inflight.get(relativePath);
  if (existing) return existing as Promise<T>;

  const promise = fetchFromBases<T>(dataBases(), relativePath, (url) => fetchJson<T>(url, signal)).catch((err) => {
    inflight.delete(relativePath);
    throw err;
  });
  inflight.set(relativePath, promise);
  return promise;
}

/** One data file by URL; a response that is not ok becomes an ApiClientError with its status. */
export async function fetchJson<T>(url: string, signal?: AbortSignal): Promise<T> {
  const response = await fetch(url, { signal, headers: { Accept: "application/json" } });
  if (!response.ok) {
    throw new ApiClientError(response.status, {
      error: response.status === 404 ? "Not found" : `Game data request failed (${response.status})`,
    });
  }
  return (await response.json()) as T;
}

/**
 * `relativePath` from the first of `bases` that serves it. A 404 and an abort are final; any
 * other failure tries the next base, and the last failure is thrown when none serves the file.
 */
export async function fetchFromBases<T>(
  bases: readonly string[],
  relativePath: string,
  fetchOne: (url: string) => Promise<T>,
): Promise<T> {
  let lastError: unknown = new Error(`Nowhere to read ${relativePath} from`);
  for (const base of bases) {
    try {
      return await fetchOne(`${base}/${relativePath}`);
    } catch (err) {
      if (isFinal(err)) throw err;
      lastError = err;
    }
  }
  throw lastError;
}

function isFinal(err: unknown): boolean {
  if (err instanceof ApiClientError) return err.status === 404;
  return typeof err === "object" && err !== null && (err as { name?: unknown }).name === "AbortError";
}
