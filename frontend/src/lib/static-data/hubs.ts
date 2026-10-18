import type { CraftType } from "@/types/api";
import { QUICK_FILTERS } from "./search";

// The browse hubs: one page per job and one per job and level range, built at deploy time from
// the exported recipe list. Pure functions, so the pages and the sitemap share them and vitest
// can pin them.

/** What a hub needs about a recipe; RecipeListEntry from static-data/server satisfies it. */
export interface HubRecipe {
  recipeId: number;
  itemName: string;
  iconUrl: string;
  itemLevel: number;
  craftType: string;
  /** The class job level the recipe unlocks at (not the rlvl). */
  classJobLevel: number;
  stars: number;
}

export interface LevelBracket {
  /** The URL segment: "91-100". */
  slug: string;
  min: number;
  max: number;
  /** "Lv. 91–100" */
  label: string;
  expansion: string;
}

/** The eight Disciples of the Hand, in the game's order. */
export const CRAFT_JOBS: readonly CraftType[] = ["CRP", "BSM", "ARM", "GSM", "LTW", "WVR", "ALC", "CUL"];

/** Ten levels per expansion after A Realm Reborn's fifty. */
export const LEVEL_BRACKETS: readonly LevelBracket[] = [
  { slug: "1-50", min: 1, max: 50, label: "Lv. 1–50", expansion: "A Realm Reborn" },
  { slug: "51-60", min: 51, max: 60, label: "Lv. 51–60", expansion: "Heavensward" },
  { slug: "61-70", min: 61, max: 70, label: "Lv. 61–70", expansion: "Stormblood" },
  { slug: "71-80", min: 71, max: 80, label: "Lv. 71–80", expansion: "Shadowbringers" },
  { slug: "81-90", min: 81, max: 90, label: "Lv. 81–90", expansion: "Endwalker" },
  { slug: "91-100", min: 91, max: 100, label: "Lv. 91–100", expansion: "Dawntrail" },
];

/** The newest bracket; the Dawntrail hub and the recipes page's quick filter agree on its floor. */
export const LATEST_BRACKET = LEVEL_BRACKETS[LEVEL_BRACKETS.length - 1];
export const DAWNTRAIL_MIN_LEVEL: number = QUICK_FILTERS.dawntrail.minLevel;

/** The job's URL segment: "crp". */
export function jobSlug(craftType: CraftType): string {
  return craftType.toLowerCase();
}

/** Reads a job segment; only the exact lower-case codes are job URLs. */
export function parseJobSlug(segment: string): CraftType | null {
  const upper = segment.toUpperCase();
  if (segment !== segment.toLowerCase()) return null;
  return (CRAFT_JOBS as readonly string[]).includes(upper) ? (upper as CraftType) : null;
}

export function findBracket(slug: string): LevelBracket | null {
  return LEVEL_BRACKETS.find((bracket) => bracket.slug === slug) ?? null;
}

/** The bracket a class job level falls in; anything below 1 counts as the first, above 100 as the last. */
export function bracketFor(classJobLevel: number): LevelBracket {
  return LEVEL_BRACKETS.find((bracket) => classJobLevel <= bracket.max) ?? LATEST_BRACKET;
}

export function recipesForJob<T extends HubRecipe>(recipes: readonly T[], craftType: CraftType): T[] {
  return recipes.filter((recipe) => recipe.craftType === craftType);
}

export function recipesInBracket<T extends HubRecipe>(recipes: readonly T[], bracket: LevelBracket): T[] {
  return recipes.filter((recipe) => bracketFor(recipe.classJobLevel) === bracket);
}

/** Hub order: by level, then stars, then name (plain code-point order, the same on server and client), then id. */
export function sortForHub<T extends HubRecipe>(recipes: readonly T[]): T[] {
  return [...recipes].sort(
    (a, b) =>
      a.classJobLevel - b.classJobLevel ||
      a.stars - b.stars ||
      (a.itemName < b.itemName ? -1 : a.itemName > b.itemName ? 1 : 0) ||
      a.recipeId - b.recipeId,
  );
}

/** Groups an already sorted list by class job level, preserving order. */
export function groupByLevel<T extends HubRecipe>(recipes: readonly T[]): { level: number; recipes: T[] }[] {
  const groups: { level: number; recipes: T[] }[] = [];
  for (const recipe of recipes) {
    const last = groups[groups.length - 1];
    if (last && last.level === recipe.classJobLevel) last.recipes.push(recipe);
    else groups.push({ level: recipe.classJobLevel, recipes: [recipe] });
  }
  return groups;
}

export function countByJob(recipes: readonly HubRecipe[]): Record<CraftType, number> {
  const counts = Object.fromEntries(CRAFT_JOBS.map((job) => [job, 0])) as Record<CraftType, number>;
  for (const recipe of recipes) {
    if (recipe.craftType in counts) counts[recipe.craftType as CraftType] += 1;
  }
  return counts;
}

/** Every (job, bracket) pair, the static params of the bracket pages. */
export function hubParams(): { job: string; bracket: string }[] {
  return CRAFT_JOBS.flatMap((job) => LEVEL_BRACKETS.map((bracket) => ({ job: jobSlug(job), bracket: bracket.slug })));
}
