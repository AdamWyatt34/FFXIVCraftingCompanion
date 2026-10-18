import { ApiClientError } from "@/lib/api-errors";
import { dataBasePath } from "./paths";

// Browser-side reads of the exported game data. Files are fetched once per page load and kept;
// the service worker keeps them across loads.

const inflight = new Map<string, Promise<unknown>>();

export function loadJson<T>(relativePath: string, signal?: AbortSignal): Promise<T> {
  const existing = inflight.get(relativePath);
  if (existing) return existing as Promise<T>;

  const promise = fetchJson<T>(`${dataBasePath()}/${relativePath}`, signal).catch((err) => {
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
