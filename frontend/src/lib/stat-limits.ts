import type { CrafterStatsInput } from "@/types/simulator";

/** Bounds for crafter stats arriving from outside the app (shared links, URL parameters). */
export const STAT_LIMITS = {
  level: { min: 1, max: 100 },
  craftsmanship: { min: 0, max: 9_999 },
  control: { min: 0, max: 9_999 },
  cp: { min: 0, max: 9_999 },
} as const;

function inRange(value: number, bounds: { min: number; max: number }): boolean {
  return Number.isInteger(value) && value >= bounds.min && value <= bounds.max;
}

/** The reason the stats cannot be used, or null when they are all within bounds. */
export function validateCrafterStats(stats: CrafterStatsInput): string | null {
  if (!inRange(stats.level, STAT_LIMITS.level)) return "Level must be between 1 and 100.";
  if (!inRange(stats.craftsmanship, STAT_LIMITS.craftsmanship)) return "Craftsmanship must be between 0 and 9999.";
  if (!inRange(stats.control, STAT_LIMITS.control)) return "Control must be between 0 and 9999.";
  if (!inRange(stats.cp, STAT_LIMITS.cp)) return "CP must be between 0 and 9999.";
  return null;
}

export function isValidCrafterStats(stats: CrafterStatsInput): boolean {
  return validateCrafterStats(stats) === null;
}
