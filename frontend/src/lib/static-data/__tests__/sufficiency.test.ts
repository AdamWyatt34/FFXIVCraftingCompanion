import { describe, expect, it } from "vitest";
import type { StatSufficiencyResultDto } from "@/types/journey";
import type { CrafterStatsInput } from "@/types/simulator";
import type { GearJobExport, ItemExport, RecipeExport, StatWeightProfileExport } from "../types";
import { recommendGear } from "../gear";
import { analyzeSufficiency, type GearSource, type SufficiencyEngine, type SufficiencyProfile } from "../sufficiency";
import { readGoldenJson, readGoldenLines } from "./goldens";

interface SufficiencyGolden {
  recipeId: number;
  level: number;
  craftsmanship: number;
  control: number;
  cp: number;
  profileName: string;
  profile: SufficiencyProfile | null;
  result: StatSufficiencyResultDto;
}

const recipes = new Map(readGoldenLines<RecipeExport>("recipe-export.jsonl").map((r) => [r.recipe.recipeId, r]));
const items = new Map(readGoldenLines<ItemExport>("item-export.jsonl").map((i) => [i.item.itemId, i]));
const profiles = readGoldenJson<StatWeightProfileExport[]>("gear-profiles.json");
const jobs = new Map(readGoldenLines<GearJobExport>("gear-export.jsonl").map((j) => [j.job, j]));

const gear: GearSource = {
  recommend: async (job, level) => recommendGear(jobs.get(job)!, profiles, level, "balanced", false),
  equipment: async (ids) =>
    new Map(ids.flatMap((id) => (items.get(id)?.equipment ? [[id, items.get(id)!.equipment!] as const] : []))),
};

// The golden generator's solver factory returns a completed solve with no rotation.
const noRotation: SufficiencyEngine = {
  solve: async () => ({ completed: true, actionIds: [] }),
  simulate: async () => {
    throw new Error("A solve without a rotation is not simulated.");
  },
};

const stats: CrafterStatsInput = { level: 100, craftsmanship: 5000, control: 5000, cp: 700 };

describe("stat sufficiency", () => {
  it.each(
    readGoldenLines<SufficiencyGolden>("sufficiency.jsonl").map(
      (g) => [`recipe ${g.recipeId} L${g.level} ${g.craftsmanship}/${g.control}/${g.cp} ${g.profileName}`, g] as const,
    ),
  )("matches the C# service for %s", async (_name, golden) => {
    const data = recipes.get(golden.recipeId)!;
    const result = await analyzeSufficiency(
      data.recipe,
      data.craftingParameters,
      { level: golden.level, craftsmanship: golden.craftsmanship, control: golden.control, cp: golden.cp },
      golden.profile,
      noRotation,
      gear,
    );
    expect(result).toEqual(golden.result);
  });

  const data = recipes.get(35423)!;
  const engineWith = (hq: number, success = true): SufficiencyEngine => ({
    solve: async () => ({ completed: true, actionIds: [1, 2, 3] }),
    simulate: async () => ({ success, totalSteps: 3, finalQualityPercent: hq, finalHQPercent: hq }),
  });

  it.each([
    [80, "StatsExcellent"],
    [79, "StatsGood"],
    [60, "StatsGood"],
    [59, "StatsAdequate"],
    [0, "StatsAdequate"],
  ])("rates an HQ chance of %s%% as %s", async (hq, recommendation) => {
    const result = await analyzeSufficiency(data.recipe, data.craftingParameters, stats, null, engineWith(hq), gear);
    expect(result).toMatchObject({ estimatedHQPercent: hq, estimatedSteps: 3, canComplete: true, recommendation });
  });

  it("recommends gear when the rotation does not complete the craft", async () => {
    const result = await analyzeSufficiency(data.recipe, data.craftingParameters, stats, null, engineWith(90, false), gear);
    expect(result).toMatchObject({ canComplete: false, recommendation: "UpgradeGear" });
  });

  it("does not solve for stats under the recipe minimums", async () => {
    const engine: SufficiencyEngine = {
      solve: async () => {
        throw new Error("The service solves only once the minimums are met.");
      },
      simulate: noRotation.simulate,
    };
    const result = await analyzeSufficiency(data.recipe, data.craftingParameters, { ...stats, craftsmanship: 1 }, null, engine, gear);
    expect(result).toMatchObject({ meetsMinimumRequirements: false, craftsmanshipSufficient: false, recommendation: "UpgradeGear" });
  });
});
