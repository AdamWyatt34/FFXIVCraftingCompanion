import { ApiClientError } from "@/lib/api-errors";
import { searchGearIndex } from "@/lib/static-data/gear";
import { searchIndex } from "@/lib/static-data/search";
import { loadSearchIndex } from "@/lib/static-data/search-compact";
import type { SearchIndexEntry } from "@/lib/static-data/types";
import type { SearchWorkerRequest, SearchWorkerResponse } from "@/types/search-worker";

export interface SearchWorkerHost {
  /** A JSON file by URL; throws ApiClientError with the HTTP status when the response is not ok. */
  fetchJson<T>(url: string): Promise<T>;
  post(message: SearchWorkerResponse): void;
}

/**
 * What the search worker does, without the worker: one index per data set, loaded on the first
 * request and kept, and the same pure search functions the main thread would otherwise run.
 */
export function createSearchWorkerCore(host: SearchWorkerHost) {
  const indexes = new Map<string, Promise<SearchIndexEntry[]>>();

  function index(base: string): Promise<SearchIndexEntry[]> {
    let loading = indexes.get(base);
    if (!loading) {
      loading = loadSearchIndex((path) => host.fetchJson(`${base}/${path}`)).catch((err) => {
        indexes.delete(base);
        throw err;
      });
      indexes.set(base, loading);
    }
    return loading;
  }

  async function handle(request: SearchWorkerRequest): Promise<void> {
    try {
      const entries = await index(request.base);
      if (request.type === "items") {
        const { query, page, pageSize, craftType, quickFilter } = request;
        host.post({ type: "items", id: request.id, result: searchIndex(entries, query, page, pageSize, craftType, quickFilter) });
      } else {
        host.post({ type: "gear", id: request.id, itemIds: searchGearIndex(entries, request.params).map((e) => e.itemId) });
      }
    } catch (err) {
      host.post({
        type: "error",
        id: request.id,
        status: err instanceof ApiClientError ? err.status : 500,
        message: err instanceof Error ? err.message : String(err),
      });
    }
  }

  return { handle };
}
