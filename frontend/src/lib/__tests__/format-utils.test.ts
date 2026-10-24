import { describe, expect, it } from "vitest";
import { SIMULATOR_CONSTANTS } from "@/lib/constants";
import { buildShareUrl, parseRotationFromUrl } from "../format-utils";
import { decodeShareContext } from "../rotation-codec";

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
    expect(parseRotationFromUrl("16777216")).toBeNull();
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

  it("round-trips the engine's six-digit action ids", () => {
    const actionIds = [100379, 100278, 100297, 100001, 100002, 100411, 100339, 100451];
    const url = new URL(
      buildShareUrl({ recipeId: 7, stats: { craftsmanship: 1, control: 2, cp: 3, level: 4 }, actionIds }),
      "http://localhost",
    );
    expect(parseRotationFromUrl(url.searchParams.get("rotation"))).toEqual(actionIds);
  });

  it("carries the share context in the same params as a solver link, only when set", () => {
    const share = (context?: Parameters<typeof buildShareUrl>[0]["context"]) =>
      new URL(
        buildShareUrl({ recipeId: 7, stats: { craftsmanship: 1, control: 2, cp: 3, level: 4 }, actionIds: [5], context }),
        "http://localhost",
      ).searchParams;
    const contextParams = ["meal", "mealHq", "medicine", "medicineHq", "sp", "sq"];

    for (const params of [
      share(),
      share({ consumables: { meal: null, medicine: null }, isSpecialist: false, startingQuality: 0 }),
    ]) {
      expect(contextParams.filter((name) => params.has(name))).toEqual([]);
    }

    const context = {
      consumables: { meal: { itemId: 44091, hq: false }, medicine: { itemId: 44170, hq: true } },
      isSpecialist: true,
      startingQuality: 75,
    };
    const params = share(context);
    expect(params.get("craftsmanship")).toBe("1");
    expect(contextParams.filter((name) => params.has(name))).toEqual(["meal", "medicine", "medicineHq", "sp", "sq"]);
    expect(decodeShareContext(params)).toEqual(context);
  });
});
