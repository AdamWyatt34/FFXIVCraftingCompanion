import { beforeEach, describe, expect, it, vi } from "vitest";
import { ApiClientError } from "@/lib/api-errors";
import type { CraftingConsumableDto } from "@/types/api";
import type { ConsumableSelection, CrafterStatsInput } from "@/types/simulator";
import {
  applyConsumables,
  consumableStatBonuses,
  CONSUMABLES_TIMEOUT_MS,
  getCraftingConsumables,
  NO_CONSUMABLES,
  sanitizeSelection,
  type ConsumableStatBonuses,
} from "../consumables";
import { loadJson } from "../fetch";
import { readGoldenJson } from "./goldens";

vi.mock("../fetch", () => ({ loadJson: vi.fn() }));

interface ConsumableChoiceGolden {
  consumable: CraftingConsumableDto;
  hq: boolean;
}

interface ConsumableCaseGolden {
  name: string;
  base: CrafterStatsInput;
  meal: ConsumableChoiceGolden | null;
  medicine: ConsumableChoiceGolden | null;
  mealBonus: ConsumableStatBonuses;
  medicineBonus: ConsumableStatBonuses;
  effective: CrafterStatsInput;
}

const { cases } = readGoldenJson<{ cases: ConsumableCaseGolden[] }>("consumable-stats.json");
const NONE: ConsumableStatBonuses = { craftsmanship: 0, control: 0, cp: 0 };

const steak: CraftingConsumableDto = {
  itemId: 44091,
  name: "Rroneek Steak",
  iconUrl: "",
  itemLevel: 710,
  canBeHQ: true,
  kind: "Meal",
  bonuses: [
    { stat: "Control", isRelative: true, value: 4, max: 77, valueHQ: 5, maxHQ: 97 },
    { stat: "CP", isRelative: true, value: 21, max: 73, valueHQ: 26, maxHQ: 92 },
  ],
};
const draught: CraftingConsumableDto = {
  itemId: 44170,
  name: "Craftsman's Draught",
  iconUrl: "",
  itemLevel: 720,
  canBeHQ: false,
  kind: "Medicine",
  bonuses: [{ stat: "CP", isRelative: false, value: 21, max: 0, valueHQ: 21, maxHQ: 0 }],
};
const options = [steak, draught];
const base: CrafterStatsInput = { level: 100, craftsmanship: 4900, control: 4800, cp: 620, isSpecialist: true };

describe("consumable stats", () => {
  it.each(cases.map((c) => [c.name, c] as const))("%s matches ConsumableStats.cs", (_name, golden) => {
    const choices = [golden.meal, golden.medicine].filter((c): c is ConsumableChoiceGolden => c !== null);
    const selection: ConsumableSelection = {
      meal: golden.meal && { itemId: golden.meal.consumable.itemId, hq: golden.meal.hq },
      medicine: golden.medicine && { itemId: golden.medicine.consumable.itemId, hq: golden.medicine.hq },
    };

    expect(golden.meal ? consumableStatBonuses(golden.base, golden.meal.consumable, golden.meal.hq) : NONE).toEqual(golden.mealBonus);
    expect(golden.medicine ? consumableStatBonuses(golden.base, golden.medicine.consumable, golden.medicine.hq) : NONE).toEqual(
      golden.medicineBonus,
    );
    expect(applyConsumables(golden.base, selection, choices.map((c) => c.consumable))).toEqual(golden.effective);
  });

  it("replays a golden that mixes HQ and NQ choices and hits a cap", () => {
    expect(cases.some((c) => c.meal?.hq && c.medicine?.hq === false)).toBe(true);
    expect(cases.some((c) => c.mealBonus.control === 97)).toBe(true);
  });

  it("leaves the base unchanged with nothing selected", () => {
    expect(applyConsumables(base, NO_CONSUMABLES, options)).toEqual(base);
  });

  it("adds nothing for an item id the catalogue lacks", () => {
    const selection: ConsumableSelection = { meal: { itemId: 1, hq: true }, medicine: { itemId: 44170, hq: false } };
    expect(applyConsumables(base, selection, options)).toEqual({ ...base, cp: 641 });
  });

  it("adds nothing for an item placed in the other kind's slot", () => {
    const selection: ConsumableSelection = { meal: { itemId: 44170, hq: false }, medicine: { itemId: 44091, hq: true } };
    expect(applyConsumables(base, selection, options)).toEqual(base);
  });

  it("reads the HQ values for an HQ meal", () => {
    const selection: ConsumableSelection = { meal: { itemId: 44091, hq: true }, medicine: null };
    expect(applyConsumables(base, selection, options)).toEqual({ ...base, control: 4897, cp: 712 });
  });
});

describe("sanitizeSelection", () => {
  it("keeps a selection the catalogue honours as the same object", () => {
    const selection: ConsumableSelection = { meal: { itemId: 44091, hq: true }, medicine: { itemId: 44170, hq: false } };
    const result = sanitizeSelection(selection, options);
    expect(result.selection).toBe(selection);
    expect(result.dropped).toEqual([]);
  });

  it("keeps an empty selection", () => {
    expect(sanitizeSelection(NO_CONSUMABLES, options)).toEqual({ selection: NO_CONSUMABLES, dropped: [] });
  });

  it("drops an item id the catalogue lacks", () => {
    const result = sanitizeSelection({ meal: { itemId: 99999, hq: false }, medicine: { itemId: 44170, hq: false } }, options);
    expect(result).toEqual({ selection: { meal: null, medicine: { itemId: 44170, hq: false } }, dropped: [99999] });
  });

  it("drops HQ on an item that has no HQ", () => {
    const result = sanitizeSelection({ meal: null, medicine: { itemId: 44170, hq: true } }, options);
    expect(result).toEqual({ selection: NO_CONSUMABLES, dropped: [44170] });
  });

  it("drops an item placed in the other kind's slot", () => {
    const result = sanitizeSelection({ meal: { itemId: 44170, hq: false }, medicine: { itemId: 44091, hq: false } }, options);
    expect(result).toEqual({ selection: NO_CONSUMABLES, dropped: [44170, 44091] });
  });

  it("drops everything when the catalogue is empty", () => {
    const result = sanitizeSelection({ meal: { itemId: 44091, hq: true }, medicine: null }, []);
    expect(result).toEqual({ selection: NO_CONSUMABLES, dropped: [44091] });
  });
});

describe("getCraftingConsumables", () => {
  // A block body: a function returned from beforeEach runs as its teardown, which would call the mock.
  beforeEach(() => {
    vi.mocked(loadJson).mockReset();
  });

  it("reads consumables.json", async () => {
    vi.mocked(loadJson).mockResolvedValueOnce(options);
    await expect(getCraftingConsumables()).resolves.toEqual(options);
    expect(vi.mocked(loadJson).mock.calls[0][0]).toBe("consumables.json");
  });

  it("answers an empty catalogue for a data set without the file", async () => {
    vi.mocked(loadJson).mockRejectedValueOnce(new ApiClientError(404, { error: "Not found" }));
    await expect(getCraftingConsumables()).resolves.toEqual([]);
  });

  it("propagates any other failure", async () => {
    const failure = new ApiClientError(503, { error: "Game data request failed (503)" });
    vi.mocked(loadJson).mockRejectedValueOnce(failure);
    await expect(getCraftingConsumables()).rejects.toBe(failure);
  });

  it("rejects with a TimeoutError when the response never arrives", async () => {
    const actual = await vi.importActual<typeof import("../fetch")>("../fetch");
    vi.mocked(loadJson).mockImplementation(actual.loadJson);
    // Like fetch, it settles only when its signal aborts, with the signal's reason.
    vi.stubGlobal(
      "fetch",
      (_url: string, init: RequestInit) =>
        new Promise((_resolve, reject) => init.signal!.addEventListener("abort", () => reject(init.signal!.reason))),
    );
    vi.useFakeTimers();
    try {
      let settled = false;
      const request = getCraftingConsumables();
      request.then(
        () => (settled = true),
        () => (settled = true),
      );

      await vi.advanceTimersByTimeAsync(CONSUMABLES_TIMEOUT_MS - 1);
      expect(settled).toBe(false);
      await vi.advanceTimersByTimeAsync(1);
      await expect(request).rejects.toMatchObject({ name: "TimeoutError" });
    } finally {
      vi.useRealTimers();
      vi.unstubAllGlobals();
    }
  });
});
