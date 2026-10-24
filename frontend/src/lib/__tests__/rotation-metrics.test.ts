import { describe, expect, it, vi } from "vitest";
import { rotationMetricsFor, rotationSaveErrorMessage } from "@/lib/rotation-metrics";
import { ApiClientError } from "@/lib/api-client";
import type { simulateCraft } from "@/lib/crafting-engine";
import type { SimulationResult } from "@/types/simulator";

const stats = { level: 100, craftsmanship: 4200, control: 4100, cp: 600, isSpecialist: true };

describe("rotationSaveErrorMessage", () => {
  it.each<[number, string]>([
    [429, "You've saved a lot of rotations recently; try again later."],
    [401, "Sign in again to save rotations."],
    [400, "The rotation was rejected; refresh the page and try again."],
    [409, "The library refused this rotation (limit reached or the rotation is invalid)."],
    [500, "Could not save the rotation."],
  ])("explains API status %s", (status, message) => {
    expect(rotationSaveErrorMessage(new ApiClientError(status, { error: "Request failed" })))
      .toBe(message);
  });

  it("explains local replay failures", () => {
    expect(rotationSaveErrorMessage(new Error("Replay failed")))
      .toBe("Could not compute this rotation's results; check your connection and try again.");
  });
});

describe("rotationMetricsFor", () => {
  it.each([true, false])("replays only the submitted setup and maps completion %s", async (success) => {
    const request = { recipeId: 37783, stats, actionIds: [100001, 100002] };
    const simulate = vi.fn<typeof simulateCraft>().mockResolvedValue({
      finalQualityPercent: 37.25,
      finalHQPercent: 19,
      totalSteps: 2,
      success,
    } as SimulationResult);

    expect(await rotationMetricsFor(request, simulate)).toEqual({
      qualityPercent: 37.25,
      hqPercent: 19,
      totalSteps: 2,
      craftCompleted: success,
    });
    expect(simulate).toHaveBeenCalledExactlyOnceWith(request);
    expect(simulate.mock.calls[0][0].stats).toBe(stats);
    expect(simulate.mock.calls[0][0].actionIds).toBe(request.actionIds);
  });

  it("uses the simulation step count for a sequence containing Final Appraisal", async () => {
    const request = { recipeId: 37783, stats, actionIds: [100364, 100001, 100001] };
    const simulate = vi.fn<typeof simulateCraft>().mockResolvedValue({
      finalQualityPercent: 0,
      finalHQPercent: 1,
      totalSteps: 2,
      success: false,
    } as SimulationResult);

    expect((await rotationMetricsFor(request, simulate)).totalSteps).toBe(2);
    expect(simulate).toHaveBeenCalledExactlyOnceWith(request);
  });
});
