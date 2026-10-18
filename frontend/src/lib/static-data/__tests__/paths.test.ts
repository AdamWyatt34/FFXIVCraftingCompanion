import { afterEach, describe, expect, it, vi } from "vitest";

// DATA_BASE is read from the environment when the module loads, so each case loads it afresh.
async function load(env: Record<string, string | undefined>) {
  vi.resetModules();
  for (const [key, value] of Object.entries(env)) {
    if (value === undefined) vi.stubEnv(key, "");
    else vi.stubEnv(key, value);
  }
  return import("../paths");
}

describe("data base path", () => {
  afterEach(() => vi.unstubAllEnvs());

  it("is the deployment's own versioned path when no CDN base was recorded", async () => {
    const paths = await load({ NEXT_PUBLIC_DATA_VERSION: "7.57-20261017-2035", NEXT_PUBLIC_DATA_BASE: "" });
    expect(paths.dataBasePath()).toBe("/data/v/7.57-20261017-2035");
  });

  it("is the CDN base when the build recorded one", async () => {
    const base = "https://cdn.jsdelivr.net/gh/AdamWyatt34/FFXIVCraftingCompanion@b84169d7";
    const paths = await load({ NEXT_PUBLIC_DATA_VERSION: "7.57-20261017-2035", NEXT_PUBLIC_DATA_BASE: base });
    expect(paths.dataBasePath()).toBe(base);
    expect(paths.recipeBucketPath(2240)).toBe("recipes/22.json");
  });
});
