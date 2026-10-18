import { describe, expect, it } from "vitest";
import { formatSpawnWindows, hourFromGameTime, parseSpawnWindows, serializeSpawnWindows } from "../spawn-windows";
import { readGoldenJson } from "./goldens";

interface SpawnWindowGolden {
  input: string | null;
  windows: { startHour: number; endHour: number }[];
  serialized: string | null;
}

describe("spawn windows", () => {
  it.each(readGoldenJson<SpawnWindowGolden[]>("spawn-windows.json").map((g) => [JSON.stringify(g.input), g] as const))(
    "parses and serializes %s like SpawnWindows.cs",
    (_name, golden) => {
      const windows = parseSpawnWindows(golden.input);
      expect(windows).toEqual(golden.windows);
      expect(serializeSpawnWindows(windows)).toBe(golden.serialized);
    },
  );

  it("maps game-data time values like SpawnWindows.HourFromGameTime", () => {
    expect(hourFromGameTime(0)).toBe(0);
    expect(hourFromGameTime(23)).toBe(23);
    expect(hourFromGameTime(2000)).toBe(20);
    expect(hourFromGameTime(2359)).toBe(23);
    for (const bad of [65535, -1, 24, 99, 2400, 1260, 1.5, Number.NaN]) expect(hourFromGameTime(bad)).toBeNull();
  });

  it("formats windows for the item page", () => {
    expect(formatSpawnWindows([])).toBe("");
    expect(formatSpawnWindows([{ startHour: 20, endHour: 0 }])).toBe("20:00–0:00 ET");
    expect(formatSpawnWindows([{ startHour: 2, endHour: 4 }, { startHour: 10, endHour: 12 }])).toBe("2:00–4:00, 10:00–12:00 ET");
    expect(formatSpawnWindows(parseSpawnWindows('["65535:00","65535:00"]'))).toBe("");
  });
});
