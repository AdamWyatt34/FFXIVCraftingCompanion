import { describe, expect, it } from "vitest";
import { applyConsumables, NO_CONSUMABLES } from "@/lib/static-data/consumables";
import type { CraftingConsumableDto } from "@/types/api";
import type { ConsumableSelection, CrafterStatsInput, HQIngredientInput } from "@/types/simulator";
import {
  craftStartKey,
  inputsForRecipe,
  inputsFromSetup,
  librarySaveProblem,
  MAX_STARTING_QUALITY,
  NO_HQ,
  NO_STARTING_QUALITY,
  replaySetup,
  shareContextOf,
  solvedSetupOf,
  startingQualityOf,
  startingQualityProblem,
  storedCraftResumes,
  storedSetup,
} from "../simulator-setup";

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
const base: CrafterStatsInput = { level: 100, craftsmanship: 4900, control: 1500, cp: 300, isSpecialist: false };
const consumables: ConsumableSelection = { meal: { itemId: 44091, hq: true }, medicine: null };

describe("replaySetup", () => {
  // Shaped like useSimulator's return value, which carries the effective stats next to the base.
  const simulator = {
    baseStats: base,
    crafterStats: applyConsumables(base, consumables, [steak]),
    consumables,
    manualStartingQuality: 120,
    hqIngredients: NO_HQ,
  };

  it("hands over the base stats and the selection, never the effective stats", () => {
    expect(simulator.crafterStats).not.toEqual(base);
    expect(replaySetup(simulator)).toEqual({
      stats: base,
      setup: { consumables, startingQuality: 120, hqIngredients: NO_HQ },
    });
  });

  it("replays to the same effective stats instead of boosting them twice", () => {
    const { stats, setup } = replaySetup(simulator);
    expect(applyConsumables(stats!, setup.consumables, [steak])).toEqual(simulator.crafterStats);
  });

  it("passes through a simulator with no stats and no consumables", () => {
    expect(
      replaySetup({ baseStats: null, consumables: NO_CONSUMABLES, manualStartingQuality: 0, hqIngredients: NO_HQ }),
    ).toEqual({
      stats: null,
      setup: { consumables: NO_CONSUMABLES, startingQuality: 0, hqIngredients: NO_HQ },
    });
  });
});

describe("starting quality", () => {
  const RECIPE = 35423;
  const QUALITY = 12900;
  const picked: HQIngredientInput[] = [{ itemId: 37830, hqQuantity: 2 }];
  const fromPicked = { ingredients: picked, value: 900 };

  it("takes a link's starting quality over HQ materials left on the same recipe", () => {
    const leftover = inputsFromSetup(RECIPE, { hqIngredients: picked }, QUALITY);
    expect(startingQualityOf(RECIPE, leftover, fromPicked).startingQuality).toBe(900);

    const link = inputsFromSetup(RECIPE, { startingQuality: 300 }, QUALITY);
    expect(startingQualityOf(RECIPE, link, fromPicked)).toEqual({
      hqIngredients: NO_HQ,
      manualStartingQuality: 300,
      startingQuality: 300,
      startingQualityStatus: "ready",
    });
  });

  it("keeps the HQ materials the simulator replays its own craft with", () => {
    const { setup } = replaySetup({ baseStats: base, consumables, manualStartingQuality: 0, hqIngredients: picked });
    expect(startingQualityOf(RECIPE, inputsFromSetup(RECIPE, setup, QUALITY), fromPicked)).toEqual({
      hqIngredients: picked,
      manualStartingQuality: 0,
      startingQuality: 900,
      startingQualityStatus: "ready",
    });
  });

  it("is pending until the HQ materials' quality answers for the current picks, and failed when it cannot", () => {
    const inputs = inputsFromSetup(RECIPE, { startingQuality: 250, hqIngredients: picked }, QUALITY);
    const repicked = { ingredients: [{ itemId: 37830, hqQuantity: 1 }], value: 450 };
    for (const unanswered of [null, repicked]) {
      expect(startingQualityOf(RECIPE, inputs, unanswered)).toMatchObject({
        startingQuality: 0,
        startingQualityStatus: "pending",
      });
    }
    expect(startingQualityOf(RECIPE, inputs, { ingredients: picked, value: null })).toMatchObject({
      startingQuality: 0,
      startingQualityStatus: "failed",
    });
    expect(startingQualityOf(RECIPE, inputs, fromPicked).startingQualityStatus).toBe("ready");
  });

  it("gives back the earlier manual value, not the HQ materials' quality, when a replayed craft goes All NQ", () => {
    for (const manualStartingQuality of [250, 0]) {
      const { setup } = replaySetup({ baseStats: base, consumables, manualStartingQuality, hqIngredients: picked });
      const loaded = inputsFromSetup(RECIPE, setup, QUALITY);
      expect(startingQualityOf(RECIPE, loaded, fromPicked).startingQuality).toBe(900);

      const allNq = { ...loaded, hq: { recipeId: RECIPE, ingredients: NO_HQ } };
      expect(startingQualityOf(RECIPE, allNq, fromPicked).startingQuality).toBe(manualStartingQuality);
    }
  });

  it("restores a saved session's manual starting quality, and its HQ materials over it", () => {
    const manual = inputsFromSetup(RECIPE, { startingQuality: 250 });
    expect(startingQualityOf(RECIPE, manual, null).startingQuality).toBe(250);

    const hq = inputsFromSetup(RECIPE, { startingQuality: 250, hqIngredients: picked });
    const restored = startingQualityOf(RECIPE, hq, fromPicked);
    expect(restored).toEqual({
      hqIngredients: picked,
      manualStartingQuality: 250,
      startingQuality: 900,
      startingQualityStatus: "ready",
    });
  });

  it("drops a manual starting quality when the recipe changes, and clamps a link's to the recipe", () => {
    const entered = { ...NO_STARTING_QUALITY, manual: { recipeId: RECIPE, value: 500 } };
    expect(startingQualityOf(37783, inputsForRecipe(entered, 37783, 33500), null).startingQuality).toBe(0);
    expect(startingQualityOf(RECIPE, inputsForRecipe(entered, RECIPE, QUALITY), null).startingQuality).toBe(500);

    const link = inputsFromSetup(null, { startingQuality: 20_000 });
    expect(startingQualityOf(RECIPE, inputsForRecipe(link, RECIPE, QUALITY), null).startingQuality).toBe(QUALITY);
  });

  it("never applies HQ materials picked for another recipe", () => {
    const other = inputsFromSetup(37783, { hqIngredients: picked });
    expect(startingQualityOf(RECIPE, other, fromPicked).hqIngredients).toBe(NO_HQ);
  });

  it("accepts only a whole number the engine's 32-bit input holds", () => {
    for (const valid of [undefined, 0, 900, MAX_STARTING_QUALITY]) expect(startingQualityProblem(valid)).toBeNull();
    for (const invalid of [-1, 1.5, Number.NaN, Number.POSITIVE_INFINITY, MAX_STARTING_QUALITY + 1, 2 ** 32]) {
      expect(startingQualityProblem(invalid)).toBe("Starting quality must be a whole number from 0 to 2147483647.");
    }
  });
});

describe("a solve's setup", () => {
  const picked: HQIngredientInput[] = [{ itemId: 37830, hqQuantity: 2 }];
  const simulator = {
    selectedRecipe: { recipeId: 35423 },
    baseStats: { ...base, isSpecialist: true },
    crafterStats: applyConsumables({ ...base, isSpecialist: true }, consumables, [steak]),
    consumables,
    appliedConsumables: consumables,
    manualStartingQuality: 250,
    hqIngredients: picked,
    startingQuality: 900,
    startingQualityStatus: "ready" as const,
  };

  it("replays and shares the setup the solve ran on, not the one edited after it", () => {
    const solved = solvedSetupOf(simulator)!;
    const edited = solvedSetupOf({
      ...simulator,
      appliedConsumables: NO_CONSUMABLES,
      manualStartingQuality: 0,
      hqIngredients: NO_HQ,
      startingQuality: 0,
    })!;

    expect(replaySetup(solved)).toEqual({
      stats: simulator.baseStats,
      setup: { consumables, startingQuality: 250, hqIngredients: picked },
    });
    expect(shareContextOf(solved)).toEqual({ consumables, isSpecialist: true, startingQuality: 900 });
    expect(solved.crafterStats).toBe(simulator.crafterStats);
    expect(replaySetup(edited).setup).not.toEqual(replaySetup(solved).setup);
    expect(shareContextOf(edited).startingQuality).toBe(0);
  });

  it("records the selection the stats carry, not a requested one the catalogue failed to apply", () => {
    const unapplied = solvedSetupOf({
      ...simulator,
      crafterStats: simulator.baseStats,
      appliedConsumables: NO_CONSUMABLES,
    })!;

    expect(simulator.consumables).not.toEqual(NO_CONSUMABLES);
    expect(unapplied.consumables).toEqual(NO_CONSUMABLES);
    expect(replaySetup(unapplied).setup.consumables).toEqual(NO_CONSUMABLES);
    expect(shareContextOf(unapplied).consumables).toEqual(NO_CONSUMABLES);
  });

  it("is null until there is a recipe and stats to solve with", () => {
    expect(solvedSetupOf({ ...simulator, selectedRecipe: null })).toBeNull();
    expect(solvedSetupOf({ ...simulator, baseStats: null, crafterStats: null })).toBeNull();
  });

  it("is null while the HQ materials' quality is pending or failed, so no share carries 0 for it", () => {
    for (const startingQualityStatus of ["pending", "failed"] as const) {
      expect(solvedSetupOf({ ...simulator, startingQuality: 0, startingQualityStatus })).toBeNull();
    }
  });
});

describe("librarySaveProblem", () => {
  it("refuses a rotation that started above zero quality, which the library would simulate from zero", () => {
    expect(librarySaveProblem(1)).toBe(
      "Saved rotations can't record a starting quality yet; share the link to keep it.",
    );
  });

  it("allows a rotation that started from zero quality", () => {
    expect(librarySaveProblem(0)).toBeUndefined();
  });
});

describe("craftStartKey", () => {
  const picked: HQIngredientInput[] = [{ itemId: 37830, hqQuantity: 2 }];

  it("tells apart starts the solver tells apart", () => {
    expect(craftStartKey({ startingQuality: 0, hqIngredients: NO_HQ })).not.toBe(
      craftStartKey({ startingQuality: 500, hqIngredients: NO_HQ }),
    );
    expect(craftStartKey({ startingQuality: 0, hqIngredients: picked })).not.toBe(
      craftStartKey({ startingQuality: 0, hqIngredients: [{ itemId: 37830, hqQuantity: 1 }] }),
    );
  });

  it("ignores the starting quality while HQ materials are picked, as the solver does", () => {
    expect(craftStartKey({ startingQuality: 0, hqIngredients: picked })).toBe(
      craftStartKey({ startingQuality: 900, hqIngredients: picked }),
    );
  });
});

describe("storedCraftResumes", () => {
  const build = "2ded861b2c84760c53e709ecd48c99042e154286";

  it("resumes a craft stored by the same engine build", () => {
    expect(storedCraftResumes({ engine: build }, build)).toBe(true);
  });

  it("closes a craft stored by another engine build, or by one that recorded none", () => {
    for (const stored of [{ engine: "0000000000000000000000000000000000000000" }, {}, { engine: null }]) {
      expect(storedCraftResumes(stored, build)).toBe(false);
    }
  });
});

describe("storedSetup", () => {
  it("keeps a well-formed stored setup", () => {
    const stored = { consumables, startingQuality: 250, hqIngredients: [{ itemId: 37830, hqQuantity: 2 }] };
    expect(storedSetup(stored)).toEqual(stored);
  });

  it("drops each part that lacks the expected shape and keeps the rest", () => {
    expect(
      storedSetup({
        consumables: { meal: { itemId: 44091, hq: "yes" }, medicine: { itemId: 27960, hq: true } },
        startingQuality: 2 ** 32,
        hqIngredients: [{ itemId: 37830, hqQuantity: -1 }, { itemId: 37831, hqQuantity: 1 }, null],
      }),
    ).toEqual({
      consumables: { meal: null, medicine: { itemId: 27960, hq: true } },
      startingQuality: 0,
      hqIngredients: [{ itemId: 37831, hqQuantity: 1 }],
    });
  });

  it("keeps HQ ids and counts only within the engine's 32-bit range", () => {
    const largest = 0x7fffffff;
    expect(
      storedSetup({
        hqIngredients: [
          { itemId: largest + 1, hqQuantity: 1 },
          { itemId: 37830, hqQuantity: largest + 1 },
          { itemId: 37831, hqQuantity: 1.5 },
          { itemId: largest, hqQuantity: largest },
        ],
      }).hqIngredients,
    ).toEqual([{ itemId: largest, hqQuantity: largest }]);
  });

  it("falls back to none for a missing or malformed setup", () => {
    expect(storedSetup({})).toEqual({ consumables: NO_CONSUMABLES, startingQuality: 0, hqIngredients: NO_HQ });
    expect(storedSetup({ consumables: "meal", startingQuality: "250", hqIngredients: { itemId: 37830 } })).toEqual({
      consumables: NO_CONSUMABLES,
      startingQuality: 0,
      hqIngredients: NO_HQ,
    });
  });
});
