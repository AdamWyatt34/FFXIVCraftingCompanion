import { readFileSync } from "node:fs";
import { join } from "node:path";
import { describe, expect, it } from "vitest";
import { ACTION_ID_TO_NAME } from "@/lib/action-icons";

const actions = JSON.parse(readFileSync(
  join(__dirname, "..", "..", "..", "..", "crates", "xiv-craft", "tests", "goldens", "actions.json"),
  "utf8",
)) as { id: number; name: string; isLegacy: boolean }[];

describe("action ids", () => {
  it("matches every id and name in the engine action catalog", () => {
    expect(ACTION_ID_TO_NAME)
      .toEqual(Object.fromEntries(actions.map((action) => [action.id, action.name])));
  });

  it("retains legacy ids for saved rotations", () => {
    const legacyIds = actions.filter((action) => action.isLegacy).map((action) => action.id);
    expect(legacyIds).toEqual(expect.arrayContaining([100235, 100243, 100252]));
    for (const id of legacyIds) expect(ACTION_ID_TO_NAME[id]).toBeDefined();
  });
});
