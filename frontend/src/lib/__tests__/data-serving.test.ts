import { describe, expect, it } from "vitest";
import { decideServing, defaultDataBase } from "../../../scripts/data-serving.mjs";

describe("data serving", () => {
  const base = defaultDataBase("AdamWyatt34/FFXIVCraftingCompanion", "b84169d761c473603b0f29e52fb9104887109af7");

  it("pins the CDN base to the data branch commit", () => {
    expect(base).toBe("https://cdn.jsdelivr.net/gh/AdamWyatt34/FFXIVCraftingCompanion@b84169d761c473603b0f29e52fb9104887109af7");
  });

  it("serves from the CDN only when it answers with the locked data set", () => {
    expect(decideServing({ forceLocal: false, base, probedVersion: "7.57-20261017-2035", expectedVersion: "7.57-20261017-2035" }))
      .toEqual({ mode: "remote", reason: null });
  });

  it("falls back to the deployment when the CDN cannot be read, for example while the repository is private", () => {
    const decision = decideServing({ forceLocal: false, base, probedVersion: null, expectedVersion: "7.57-20261017-2035" });
    expect(decision.mode).toBe("local");
    expect(decision.reason).toContain(`${base}/manifest.json could not be read`);
  });

  it("falls back when the CDN serves a different data set than the lock", () => {
    const decision = decideServing({ forceLocal: false, base, probedVersion: "7.56-20261010-2000", expectedVersion: "7.57-20261017-2035" });
    expect(decision.mode).toBe("local");
    expect(decision.reason).toContain("7.56-20261010-2000");
    expect(decision.reason).toContain("7.57-20261017-2035");
  });

  it("can be forced local regardless of the CDN", () => {
    expect(decideServing({ forceLocal: true, base, probedVersion: "7.57-20261017-2035", expectedVersion: "7.57-20261017-2035" }))
      .toEqual({ mode: "local", reason: "forced" });
  });
});
