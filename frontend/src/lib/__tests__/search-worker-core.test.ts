import { describe, expect, it } from "vitest";
import { ApiClientError } from "@/lib/api-errors";
import { createSearchWorkerCore } from "@/lib/search-worker-core";
import { readGoldenJson } from "@/lib/static-data/__tests__/goldens";
import { searchGearIndex } from "@/lib/static-data/gear";
import { searchIndex } from "@/lib/static-data/search";
import { inflateSearchIndex } from "@/lib/static-data/search-compact";
import type { CompactSearchIndex } from "@/lib/static-data/types";
import type { ItemSearchRequest, SearchWorkerResponse } from "@/types/search-worker";

function host(files: Record<string, unknown>) {
  const posted: SearchWorkerResponse[] = [];
  const fetched: string[] = [];
  const core = createSearchWorkerCore({
    fetchJson<T>(url: string): Promise<T> {
      fetched.push(url);
      if (url in files) return Promise.resolve(files[url] as T);
      return Promise.reject(new ApiClientError(404, { error: "Not found" }));
    },
    post: (message) => posted.push(message),
  });
  return { core, posted, fetched };
}

function items(id: string, base: string, query: string, page = 1, pageSize = 20, craftType: string | null = null): ItemSearchRequest {
  return { type: "items", id, base, query, page, pageSize, craftType, quickFilter: null };
}

describe("search worker core", () => {
  const compact = readGoldenJson<CompactSearchIndex>("search-compact.json");
  const entries = inflateSearchIndex(compact);

  it("answers item searches from the compact index, loaded once per data set", async () => {
    const { core, posted, fetched } = host({ "/data/v/1/search/compact.json": compact });

    await core.handle(items("a", "/data/v/1", "ingot"));
    await core.handle(items("b", "/data/v/1", "ing", 2, 5, "ARM"));

    expect(posted).toEqual([
      { type: "items", id: "a", result: searchIndex(entries, "ingot", 1, 20, null, null) },
      { type: "items", id: "b", result: searchIndex(entries, "ing", 2, 5, "ARM", null) },
    ]);
    expect(fetched).toEqual(["/data/v/1/search/compact.json"]);
  });

  it("falls back to the entries file of a data set exported before the compact index", async () => {
    const { core, posted, fetched } = host({ "/data/v/0/search/items.json": entries });

    await core.handle(items("a", "/data/v/0", "ingot"));

    expect(fetched).toEqual(["/data/v/0/search/compact.json", "/data/v/0/search/items.json"]);
    expect(posted).toEqual([{ type: "items", id: "a", result: searchIndex(entries, "ingot", 1, 20, null, null) }]);
  });

  it("answers gear searches with the matching item ids in order", async () => {
    const { core, posted } = host({ "/data/v/1/search/compact.json": compact });
    const params = { query: "", job: "CRP" };

    await core.handle({ type: "gear", id: "g", base: "/data/v/1", params });

    const expected = searchGearIndex(entries, params).map((e) => e.itemId);
    expect(expected.length).toBeGreaterThan(0);
    expect(posted).toEqual([{ type: "gear", id: "g", itemIds: expected }]);
  });

  it("reports a data set without an index as an error and does not keep the failed load", async () => {
    const { core, posted, fetched } = host({});

    await core.handle(items("a", "/data/v/x", "ingot"));
    await core.handle(items("b", "/data/v/x", "ingot"));

    expect(posted).toEqual([
      { type: "error", id: "a", status: 404, message: "Not found" },
      { type: "error", id: "b", status: 404, message: "Not found" },
    ]);
    expect(fetched).toHaveLength(4);
  });
});
