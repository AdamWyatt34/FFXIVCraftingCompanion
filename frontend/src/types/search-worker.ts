import type { ItemSearchResultDto } from "./api";
import type { GearSearchParams } from "@/lib/static-data/gear";
import type { QuickFilter } from "@/lib/static-data/search";

/**
 * Messages between the main thread and the search worker (`src/workers/search.worker.ts`). The
 * worker loads the search index of the data set at `base` on its first request and keeps it.
 */

export interface ItemSearchRequest {
  type: "items";
  id: string;
  base: string;
  query: string | null;
  page: number;
  pageSize: number;
  craftType: string | null;
  quickFilter: QuickFilter | null;
}

export interface GearSearchRequest {
  type: "gear";
  id: string;
  base: string;
  params: GearSearchParams;
}

export type SearchWorkerRequest = ItemSearchRequest | GearSearchRequest;

export type SearchWorkerResponse =
  | { type: "items"; id: string; result: ItemSearchResultDto }
  | { type: "gear"; id: string; itemIds: number[] }
  | { type: "error"; id: string; status: number; message: string };
