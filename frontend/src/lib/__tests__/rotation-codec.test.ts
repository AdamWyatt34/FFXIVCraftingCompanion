import { describe, expect, it } from "vitest";
import {
  MAX_SHARED_ACTIONS,
  buildSolverShareUrl,
  decodeRotation,
  decodeShareContext,
  encodeRotation,
  encodeShareContext,
  parseSolverShareUrl,
  type ShareContext,
} from "../rotation-codec";

const NO_CONTEXT: ShareContext = {
  consumables: { meal: null, medicine: null },
  isSpecialist: false,
  startingQuality: 0,
};

// Ids from the engine's action registry (Muscle Memory, Manipulation, Veneration, ...).
const ROTATION_30 = [
  100379, 100278, 100297, 100285, 100403, 100403, 100323, 100323, 100209, 100227,
  100002, 100004, 100411, 100209, 100299, 100299, 100260, 100387, 100002, 100004,
  100411, 100260, 100209, 100355, 100371, 100003, 100339, 100364, 100001, 100001,
];

const base64Url = (bytes: number[]) =>
  btoa(String.fromCharCode(...bytes)).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");

describe("rotation codec", () => {
  it("round-trips narrow ids", () => {
    const ids = [1, 2, 3, 100, 255];
    expect(decodeRotation(encodeRotation(ids))).toEqual(ids);
  });

  it("round-trips wide ids", () => {
    const ids = [1, 300, 65535];
    expect(decodeRotation(encodeRotation(ids))).toEqual(ids);
  });

  it("round-trips the engine's six-digit action ids", () => {
    expect(ROTATION_30).toHaveLength(30);
    for (const ids of [[100001], [100002], [100379], [100451], [100001, 100002, 100379, 100451], ROTATION_30]) {
      expect(decodeRotation(encodeRotation(ids))).toEqual(ids);
    }
  });

  it("never lets a narrow rotation's first id read as a prefix", () => {
    for (const ids of [[254, 1], [255, 1], [253, 1]]) {
      expect(decodeRotation(encodeRotation(ids))).toEqual(ids);
    }
  });

  it("rejects junk, empty and oversized rotations", () => {
    expect(decodeRotation("")).toBeNull();
    expect(decodeRotation("!!!not base64!!!")).toBeNull();
    expect(decodeRotation(encodeRotation([]))).toBeNull();
    expect(decodeRotation(encodeRotation(Array(MAX_SHARED_ACTIONS).fill(1)))).toHaveLength(MAX_SHARED_ACTIONS);
    expect(decodeRotation(encodeRotation(Array(MAX_SHARED_ACTIONS + 1).fill(1)))).toBeNull();
    // Wide form with a zero id, and a wide form with an odd payload (FF 00 01 02).
    expect(decodeRotation(encodeRotation([300, 0]))).toBeNull();
    expect(decodeRotation("_wABAg")).toBeNull();
  });

  it("rejects a truncated or empty 3-byte payload", () => {
    const valid = [0xfe, 0x01, 0x86, 0xa1, 0x01, 0x86, 0xa2];
    expect(decodeRotation(base64Url(valid))).toEqual([100001, 100002]);
    expect(decodeRotation(base64Url(valid.slice(0, -1)))).toBeNull();
    expect(decodeRotation(base64Url(valid.slice(0, -2)))).toBeNull();
    expect(decodeRotation(base64Url([0xfe]))).toBeNull();
    expect(decodeRotation(encodeRotation([100001, 0]))).toBeNull();
  });

  it("parses the share URL it builds, and nothing else", () => {
    const url = new URL(
      buildSolverShareUrl({
        recipeId: 37783,
        stats: { craftsmanship: 5000, control: 5000, cp: 700, level: 100 },
        actionIds: ROTATION_30,
        algorithm: "mcts",
      }),
      "http://localhost",
    );
    expect(parseSolverShareUrl(url.searchParams)).toEqual({
      recipeId: 37783,
      stats: { craftsmanship: 5000, control: 5000, cp: 700, level: 100 },
      actionIds: ROTATION_30,
      algorithm: "mcts",
      context: NO_CONTEXT,
    });

    expect(parseSolverShareUrl(new URLSearchParams("recipeId=1"))).toBeNull();
    expect(parseSolverShareUrl(new URLSearchParams("solve=1&recipeId=0&c=1&ct=1&cp=1&lv=1&r=AQ"))).toBeNull();
    expect(parseSolverShareUrl(new URLSearchParams("solve=1&recipeId=1e300&c=1&ct=1&cp=1&lv=1&r=AQ"))).toBeNull();
    expect(parseSolverShareUrl(new URLSearchParams("solve=1&recipeId=1&c=1&ct=1&cp=40000&lv=1&r=AQ"))).toBeNull();
    expect(parseSolverShareUrl(new URLSearchParams("solve=1&recipeId=1&c=1&ct=1&cp=1&lv=101&r=AQ"))).toBeNull();
    expect(parseSolverShareUrl(new URLSearchParams("solve=1&recipeId=1&c=1&ct=1&cp=1&lv=1&r=")))
      .toBeNull();
    expect(
      parseSolverShareUrl(new URLSearchParams("solve=1&recipeId=1&c=1&ct=1&cp=1&lv=1&r=AQ&alg=<script>"))?.algorithm,
    ).toBeUndefined();
  });
});

describe("share context", () => {
  const full: ShareContext = {
    consumables: { meal: { itemId: 44091, hq: true }, medicine: { itemId: 44170, hq: false } },
    isSpecialist: true,
    startingQuality: 1200,
  };

  it("round-trips through a solver link next to the base stats", () => {
    const stats = { craftsmanship: 4900, control: 4800, cp: 620, level: 100 };
    const url = new URL(buildSolverShareUrl({ recipeId: 37783, stats, actionIds: [1, 2], context: full }), "http://localhost");

    expect(url.searchParams.get("meal")).toBe("44091");
    expect(url.searchParams.get("mealHq")).toBe("1");
    expect(url.searchParams.get("medicine")).toBe("44170");
    expect(url.searchParams.has("medicineHq")).toBe(false);
    expect(url.searchParams.get("sp")).toBe("1");
    expect(url.searchParams.get("sq")).toBe("1200");
    expect(parseSolverShareUrl(url.searchParams)).toMatchObject({ stats, context: full });
  });

  it("writes nothing for an empty context", () => {
    const search = new URLSearchParams();
    encodeShareContext(search, NO_CONTEXT);
    expect(search.toString()).toBe("");
  });

  it("reads a link without context as none, not specialist and zero starting quality", () => {
    expect(decodeShareContext(new URLSearchParams("recipeId=1&c=1"))).toEqual(NO_CONTEXT);
  });

  it("empties a slot whose id is not a positive integer", () => {
    for (const id of ["", "0", "-5", "1.5", "abc", "1e3", "0x10", "2147483648"]) {
      expect(decodeShareContext(new URLSearchParams({ meal: id, medicine: id, mealHq: "1" })).consumables).toEqual({
        meal: null,
        medicine: null,
      });
    }
    expect(decodeShareContext(new URLSearchParams("mealHq=1&medicineHq=1")).consumables).toEqual({
      meal: null,
      medicine: null,
    });
  });

  it("sets HQ and specialist only for the literal 1", () => {
    for (const flag of ["true", "yes", "01", "2", ""]) {
      const ctx = decodeShareContext(new URLSearchParams({ meal: "44091", mealHq: flag, sp: flag }));
      expect(ctx.consumables.meal).toEqual({ itemId: 44091, hq: false });
      expect(ctx.isSpecialist).toBe(false);
    }
  });

  it("bounds the starting quality to [0, i32] and reads anything else as 0", () => {
    const sq = (value: string) => decodeShareContext(new URLSearchParams({ sq: value })).startingQuality;
    expect(sq("350")).toBe(350);
    expect(sq("-20")).toBe(0);
    expect(sq("2147483648")).toBe(2147483647);
    expect(sq("99999999999999999999")).toBe(2147483647);
    for (const junk of ["", "1.5", "abc", "1e9", "NaN", "Infinity"]) expect(sq(junk)).toBe(0);
  });
});
