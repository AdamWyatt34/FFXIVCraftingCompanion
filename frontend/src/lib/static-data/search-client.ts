import { ApiClientError } from "@/lib/api-errors";
import type { ItemSearchResultDto } from "@/types/api";
import type { SearchWorkerRequest, SearchWorkerResponse } from "@/types/search-worker";
import { loadJson } from "./fetch";
import type { GearSearchParams } from "./gear";
import { dataBases } from "./paths";
import type { QuickFilter } from "./search";
import { loadSearchIndex } from "./search-compact";
import type { SearchIndexEntry } from "./types";

/**
 * The item and gear searches, run in `src/workers/search.worker.ts` so the index is parsed and
 * scanned off the main thread. Where there is no Worker (the server, tests) the same functions
 * run inline over the same loader.
 */

export interface ItemSearchParams {
  query: string | null;
  page: number;
  pageSize: number;
  craftType: string | null;
  quickFilter: QuickFilter | null;
}

interface Pending {
  resolve: (value: unknown) => void;
  reject: (err: unknown) => void;
}

let worker: Worker | null = null;
const pending = new Map<string, Pending>();
let nextId = 0;

function abortError(): DOMException {
  return new DOMException("Aborted", "AbortError");
}

function failAll(err: unknown): void {
  for (const entry of pending.values()) entry.reject(err);
  pending.clear();
}

function getWorker(): Worker {
  if (worker) return worker;
  const created = new Worker(new URL("../../workers/search.worker.ts", import.meta.url));
  created.onmessage = (event: MessageEvent<SearchWorkerResponse>) => {
    const message = event.data;
    const entry = pending.get(message.id);
    if (!entry) return;
    pending.delete(message.id);
    if (message.type === "error") entry.reject(new ApiClientError(message.status, { error: message.message }));
    else if (message.type === "items") entry.resolve(message.result);
    else entry.resolve(message.itemIds);
  };
  created.onerror = (event) => {
    failAll(new ApiClientError(500, { error: event.message || "Search worker crashed" }));
    created.terminate();
    worker = null;
  };
  worker = created;
  return created;
}

/** A request before the bridge adds the id and the data set; distributes over the union. */
type Ask<R> = R extends unknown ? Omit<R, "id" | "base" | "fallbackBase"> : never;

function ask<T>(request: Ask<SearchWorkerRequest>, signal?: AbortSignal): Promise<T> {
  if (signal?.aborted) return Promise.reject(abortError());
  const id = `s${++nextId}`;
  return new Promise<T>((resolve, reject) => {
    const onAbort = () => {
      pending.delete(id);
      reject(abortError());
    };
    signal?.addEventListener("abort", onAbort, { once: true });
    const settle =
      <V>(fn: (value: V) => void) =>
      (value: V) => {
        signal?.removeEventListener("abort", onAbort);
        fn(value);
      };
    pending.set(id, { resolve: settle((value) => resolve(value as T)), reject: settle(reject) });
    const [base, fallbackBase] = dataBases();
    getWorker().postMessage({ ...request, id, base, ...(fallbackBase ? { fallbackBase } : {}) } satisfies SearchWorkerRequest);
  });
}

const hasWorker = typeof Worker !== "undefined";

/** The index for the inline path; the worker keeps its own copy. */
export function getSearchIndex(signal?: AbortSignal): Promise<SearchIndexEntry[]> {
  return loadSearchIndex((path) => loadJson(path, signal));
}

export async function searchItemsIndexed(params: ItemSearchParams, signal?: AbortSignal): Promise<ItemSearchResultDto> {
  if (hasWorker) return ask<ItemSearchResultDto>({ type: "items", ...params }, signal);
  const [{ searchIndex }, entries] = await Promise.all([import("./search"), getSearchIndex(signal)]);
  if (signal?.aborted) throw abortError();
  return searchIndex(entries, params.query, params.page, params.pageSize, params.craftType, params.quickFilter);
}

/** The matching equipment's item ids, best item level first. */
export async function searchGearItemIds(params: GearSearchParams, signal?: AbortSignal): Promise<number[]> {
  if (hasWorker) return ask<number[]>({ type: "gear", params }, signal);
  const [{ searchGearIndex }, entries] = await Promise.all([import("./gear"), getSearchIndex(signal)]);
  if (signal?.aborted) throw abortError();
  return searchGearIndex(entries, params).map((e) => e.itemId);
}
