import { describe, expect, it } from "vitest";
import { SIMULATOR_CONSTANTS } from "@/lib/constants";
import { buildShareUrl, parseRotationFromUrl } from "../format-utils";

describe("rotation URL parameter", () => {
  it("parses comma-separated positive ids", () => {
    expect(parseRotationFromUrl("1,2, 3")).toEqual([1, 2, 3]);
    expect(parseRotationFromUrl("1,,2")).toEqual([1, 2]);
  });

  it("rejects anything that is not a positive integer id", () => {
    expect(parseRotationFromUrl(null)).toBeNull();
    expect(parseRotationFromUrl("")).toBeNull();
    expect(parseRotationFromUrl("1,x")).toBeNull();
    expect(parseRotationFromUrl("0,1")).toBeNull();
    expect(parseRotationFromUrl("1.5")).toBeNull();
    expect(parseRotationFromUrl("-1")).toBeNull();
    expect(parseRotationFromUrl("70000")).toBeNull();
  });

  it("caps the number of steps at a craft's maximum", () => {
    const max = SIMULATOR_CONSTANTS.MAX_ACTIONS_PER_CRAFT;
    expect(parseRotationFromUrl(Array(max).fill("1").join(","))).toHaveLength(max);
    expect(parseRotationFromUrl(Array(max + 1).fill("1").join(","))).toBeNull();
  });

  it("builds a link the parser reads back", () => {
    const url = new URL(
      buildShareUrl({
        recipeId: 7,
        stats: { craftsmanship: 1, control: 2, cp: 3, level: 4 },
        actionIds: [5, 6],
      }),
      "http://localhost",
    );
    expect(parseRotationFromUrl(url.searchParams.get("rotation"))).toEqual([5, 6]);
  });
});
