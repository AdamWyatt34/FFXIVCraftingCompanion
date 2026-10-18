import { describe, expect, it } from "vitest";
import {
  MAX_SHARED_ACTIONS,
  buildSolverShareUrl,
  decodeRotation,
  encodeRotation,
  parseSolverShareUrl,
} from "../rotation-codec";

describe("rotation codec", () => {
  it("round-trips narrow ids", () => {
    const ids = [1, 2, 3, 100, 255];
    expect(decodeRotation(encodeRotation(ids))).toEqual(ids);
  });

  it("round-trips wide ids", () => {
    const ids = [1, 300, 65535];
    expect(decodeRotation(encodeRotation(ids))).toEqual(ids);
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

  it("parses the share URL it builds, and nothing else", () => {
    const url = new URL(
      buildSolverShareUrl({
        recipeId: 37783,
        stats: { craftsmanship: 5000, control: 5000, cp: 700, level: 100 },
        actionIds: [1, 2, 3],
        algorithm: "mcts",
      }),
      "http://localhost",
    );
    expect(parseSolverShareUrl(url.searchParams)).toEqual({
      recipeId: 37783,
      stats: { craftsmanship: 5000, control: 5000, cp: 700, level: 100 },
      actionIds: [1, 2, 3],
      algorithm: "mcts",
    });

    expect(parseSolverShareUrl(new URLSearchParams("recipeId=1"))).toBeNull();
    expect(parseSolverShareUrl(new URLSearchParams("solve=1&recipeId=0&c=1&ct=1&cp=1&lv=1&r=AQ"))).toBeNull();
    expect(parseSolverShareUrl(new URLSearchParams("solve=1&recipeId=1&c=1&ct=1&cp=40000&lv=1&r=AQ"))).toBeNull();
    expect(parseSolverShareUrl(new URLSearchParams("solve=1&recipeId=1&c=1&ct=1&cp=1&lv=101&r=AQ"))).toBeNull();
    expect(parseSolverShareUrl(new URLSearchParams("solve=1&recipeId=1&c=1&ct=1&cp=1&lv=1&r=")))
      .toBeNull();
    expect(
      parseSolverShareUrl(new URLSearchParams("solve=1&recipeId=1&c=1&ct=1&cp=1&lv=1&r=AQ&alg=<script>"))?.algorithm,
    ).toBeUndefined();
  });
});
