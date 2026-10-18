/// <reference lib="webworker" />

/**
 * The search worker: parses the search index (50k entries, 3 MB of JSON for the full data set)
 * and scans it per keystroke here, so the main thread never holds or folds it. The main thread
 * talks to it through `src/lib/static-data/search-client.ts`.
 */

import { createSearchWorkerCore } from "@/lib/search-worker-core";
import { fetchJson } from "@/lib/static-data/fetch";
import type { SearchWorkerRequest } from "@/types/search-worker";

declare const self: DedicatedWorkerGlobalScope;

const core = createSearchWorkerCore({
  fetchJson: (url) => fetchJson(url),
  post: (message) => self.postMessage(message),
});

self.onmessage = (event: MessageEvent<SearchWorkerRequest>) => {
  void core.handle(event.data);
};
