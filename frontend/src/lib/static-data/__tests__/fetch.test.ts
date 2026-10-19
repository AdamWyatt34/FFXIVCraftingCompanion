import { describe, expect, it } from "vitest";
import { ApiClientError } from "@/lib/api-errors";
import { fetchFromBases } from "../fetch";

const cdn = "https://cdn.example/gh/repo@sha";
const raw = "https://raw.example/repo/sha";

function fetcher(answers: Record<string, unknown>) {
  const calls: string[] = [];
  const fetchOne = <T>(url: string): Promise<T> => {
    calls.push(url);
    const answer = answers[url];
    return answer instanceof Error ? Promise.reject(answer) : Promise.resolve(answer as T);
  };
  return { calls, fetchOne };
}

describe("fetchFromBases", () => {
  it("reads from the first base that serves the file", async () => {
    const { calls, fetchOne } = fetcher({ [`${cdn}/recipes/329.json`]: { ok: 1 }, [`${raw}/recipes/329.json`]: { ok: 2 } });
    await expect(fetchFromBases([cdn, raw], "recipes/329.json", fetchOne)).resolves.toEqual({ ok: 1 });
    expect(calls).toEqual([`${cdn}/recipes/329.json`]);
  });

  it("moves to the fallback when the CDN refuses the file", async () => {
    const refused = new ApiClientError(403, { error: "Game data request failed (403)" });
    const { calls, fetchOne } = fetcher({ [`${cdn}/recipes/330.json`]: refused, [`${raw}/recipes/330.json`]: { ok: 2 } });
    await expect(fetchFromBases([cdn, raw], "recipes/330.json", fetchOne)).resolves.toEqual({ ok: 2 });
    expect(calls).toEqual([`${cdn}/recipes/330.json`, `${raw}/recipes/330.json`]);
  });

  it("treats a 404 as final, because every base serves the same commit", async () => {
    const missing = new ApiClientError(404, { error: "Not found" });
    const { calls, fetchOne } = fetcher({ [`${cdn}/search/compact.json`]: missing, [`${raw}/search/compact.json`]: { ok: 2 } });
    await expect(fetchFromBases([cdn, raw], "search/compact.json", fetchOne)).rejects.toBe(missing);
    expect(calls).toEqual([`${cdn}/search/compact.json`]);
  });

  it("treats an abort as final", async () => {
    const aborted = new DOMException("The operation was aborted.", "AbortError");
    const { calls, fetchOne } = fetcher({ [`${cdn}/items/1.json`]: aborted, [`${raw}/items/1.json`]: { ok: 2 } });
    await expect(fetchFromBases([cdn, raw], "items/1.json", fetchOne)).rejects.toBe(aborted);
    expect(calls).toEqual([`${cdn}/items/1.json`]);
  });

  it("throws the last failure when no base serves the file", async () => {
    const first = new ApiClientError(403, { error: "Game data request failed (403)" });
    const last = new TypeError("Failed to fetch");
    const { fetchOne } = fetcher({ [`${cdn}/items/2.json`]: first, [`${raw}/items/2.json`]: last });
    await expect(fetchFromBases([cdn, raw], "items/2.json", fetchOne)).rejects.toBe(last);
  });
});
