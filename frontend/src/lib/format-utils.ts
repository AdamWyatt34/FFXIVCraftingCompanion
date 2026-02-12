import type { StepResult, CrafterStatsInput } from "@/types/simulator";

/** Collapse consecutive identical actions: [{name, count}] */
export function collapseActions(
  steps: StepResult[],
): { name: string; count: number }[] {
  const result: { name: string; count: number }[] = [];
  for (const step of steps) {
    const last = result[result.length - 1];
    if (last && last.name === step.actionName) {
      last.count++;
    } else {
      result.push({ name: step.actionName, count: 1 });
    }
  }
  return result;
}

/** Format a compact rotation summary string with stats */
export function formatRotationSummary(
  steps: StepResult[],
  finalState: {
    progress: number;
    maxProgress: number;
    quality: number;
    maxQuality: number;
    hqPercent: number;
  },
): string {
  const collapsed = collapseActions(steps);
  const parts = collapsed.map((a) =>
    a.count > 1 ? `${a.name} \u00d7${a.count}` : a.name,
  );
  const stats = [
    `${steps.length} actions`,
    `${finalState.progress.toLocaleString()}/${finalState.maxProgress.toLocaleString()} prog`,
    `${finalState.quality.toLocaleString()}/${finalState.maxQuality.toLocaleString()} qual`,
    `${finalState.hqPercent}% HQ`,
  ].join(", ");
  return `${parts.join(" \u2192 ")}\n(${stats})`;
}

/** Build a shareable URL for a rotation */
export function buildShareUrl(params: {
  recipeId: number;
  stats: CrafterStatsInput;
  actionIds: number[];
}): string {
  const base = typeof window !== "undefined" ? window.location.origin : "";
  const search = new URLSearchParams({
    recipeId: String(params.recipeId),
    craftsmanship: String(params.stats.craftsmanship),
    control: String(params.stats.control),
    cp: String(params.stats.cp),
    level: String(params.stats.level),
    rotation: params.actionIds.join(","),
  });
  return `${base}/simulator?${search.toString()}`;
}

/** Parse a comma-separated rotation param into action IDs */
export function parseRotationFromUrl(param: string | null): number[] | null {
  if (!param) return null;
  const ids = param
    .split(",")
    .map(Number)
    .filter((n) => !isNaN(n) && n > 0);
  return ids.length > 0 ? ids : null;
}
