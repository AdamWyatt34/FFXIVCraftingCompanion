import { ApiClientError } from "@/lib/api-client";
import { simulateCraft } from "@/lib/crafting-engine";
import type { SubmitRotationRequest } from "@/types/solver";

export function rotationSaveErrorMessage(error: unknown): string {
  if (!(error instanceof ApiClientError)) {
    return "Could not compute this rotation's results; check your connection and try again.";
  }
  switch (error.status) {
    case 429:
      return "You've saved a lot of rotations recently; try again later.";
    case 401:
      return "Sign in again to save rotations.";
    case 400:
      return "The rotation was rejected; refresh the page and try again.";
    case 409:
      return "The library refused this rotation (limit reached or the rotation is invalid).";
    default:
      return "Could not save the rotation.";
  }
}

export async function rotationMetricsFor(
  {
    recipeId,
    stats,
    actionIds,
  }: Pick<SubmitRotationRequest, "recipeId" | "stats" | "actionIds">,
  simulate = simulateCraft,
): Promise<SubmitRotationRequest["metrics"]> {
  // Replay the saved action ids: recorded step history can omit step-free actions.
  const result = await simulate({ recipeId, stats, actionIds });
  return {
    qualityPercent: result.finalQualityPercent,
    hqPercent: result.finalHQPercent,
    totalSteps: result.totalSteps,
    craftCompleted: result.success,
  };
}
