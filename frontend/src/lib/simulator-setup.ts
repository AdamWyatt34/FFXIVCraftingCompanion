import type { ShareContext } from "@/lib/rotation-codec";
import { NO_CONSUMABLES } from "@/lib/static-data/consumables";
import type { ConsumableChoice, ConsumableSelection, CrafterStatsInput, HQIngredientInput } from "@/types/simulator";

export const NO_HQ: HQIngredientInput[] = [];

/** The engine takes starting quality as a 32-bit integer, where a larger or fractional value would wrap. */
export const MAX_STARTING_QUALITY = 2 ** 31 - 1;

/** The 400 message for a starting quality the engine cannot take; null for one it can, or for none. */
export function startingQualityProblem(value: number | undefined): string | null {
  if (value === undefined || (Number.isInteger(value) && value >= 0 && value <= MAX_STARTING_QUALITY)) return null;
  return `Starting quality must be a whole number from 0 to ${MAX_STARTING_QUALITY}.`;
}

/** What a craft starts from besides the base stats; a missing field means none. */
export interface SimulatorSetup {
  consumables?: ConsumableSelection;
  startingQuality?: number;
  hqIngredients?: HQIngredientInput[];
}

export interface SimulatorSetupSource {
  baseStats: CrafterStatsInput | null;
  /** The selection the stats were boosted with, never one requested but not applied: the replay applies it again. */
  consumables: ConsumableSelection;
  /** The manual value, never the HQ materials' quality: the replay works that out from `hqIngredients` again. */
  manualStartingQuality: number;
  hqIngredients: HQIngredientInput[];
}

/** Pass the base stats: the effective ones already carry the consumables, which the replay applies again. */
export function replaySetup<Source extends SimulatorSetupSource>(source: Source): {
  stats: Source["baseStats"];
  setup: Required<SimulatorSetup>;
} {
  return {
    stats: source.baseStats,
    setup: {
      consumables: source.consumables,
      startingQuality: source.manualStartingQuality,
      hqIngredients: source.hqIngredients,
    },
  };
}

/** The setup a solve ran on. Its result is shared and replayed with this, never with the live setup, which may have changed since. */
export interface SolvedSetup extends SimulatorSetupSource {
  recipeId: number;
  baseStats: CrafterStatsInput;
  /** The base stats plus the consumables: what the solver ran on. */
  crafterStats: CrafterStatsInput;
  /** The HQ materials' quality while any is picked, else the manual value. */
  startingQuality: number;
}

/** Null until the simulator has a recipe and stats to solve with, and while the HQ materials' quality is unknown: a share link would carry 0 for it. */
export function solvedSetupOf(
  simulator: Omit<SolvedSetup, "recipeId" | "baseStats" | "crafterStats" | "consumables"> & {
    selectedRecipe: { recipeId: number } | null;
    baseStats: CrafterStatsInput | null;
    crafterStats: CrafterStatsInput | null;
    appliedConsumables: ConsumableSelection;
    startingQualityStatus: StartingQualityStatus;
  },
): SolvedSetup | null {
  const { selectedRecipe, baseStats, crafterStats } = simulator;
  if (!selectedRecipe || !baseStats || !crafterStats || simulator.startingQualityStatus !== "ready") return null;
  return {
    recipeId: selectedRecipe.recipeId,
    baseStats,
    crafterStats,
    consumables: simulator.appliedConsumables,
    manualStartingQuality: simulator.manualStartingQuality,
    hqIngredients: simulator.hqIngredients,
    startingQuality: simulator.startingQuality,
  };
}

/** The library simulates a saved rotation from zero quality, so one that started higher would be stored with the wrong metrics. */
export function librarySaveProblem(startingQuality: number): string | undefined {
  return startingQuality > 0
    ? "Saved rotations can't record a starting quality yet; share the link to keep it."
    : undefined;
}

/** A link carries no HQ materials, so it carries the quality they gave instead. */
export function shareContextOf(setup: SolvedSetup): ShareContext {
  return {
    consumables: setup.consumables,
    isSpecialist: setup.baseStats.isSpecialist ?? false,
    startingQuality: setup.startingQuality,
  };
}

/** What the solver starts from: the HQ materials while any is picked, else the starting quality. */
export interface CraftStart {
  startingQuality: number;
  hqIngredients: HQIngredientInput[];
}

/** Equal exactly when the solver would start from the same quality, so a cached solve never answers for another start. */
export function craftStartKey({ startingQuality, hqIngredients }: CraftStart): string {
  return hqIngredients.length > 0
    ? `hq:${hqIngredients.map((i) => `${i.itemId}x${i.hqQuantity}`).join(",")}`
    : `sq:${startingQuality}`;
}

/** A stored session's setup without the parts that lack the expected shape, so a stale or edited entry never reaches the engine. */
export function storedSetup(stored: {
  consumables?: unknown;
  startingQuality?: unknown;
  hqIngredients?: unknown;
}): Required<SimulatorSetup> {
  const { startingQuality, hqIngredients } = stored;
  return {
    consumables: storedConsumables(stored.consumables),
    startingQuality:
      typeof startingQuality === "number" && startingQualityProblem(startingQuality) === null ? startingQuality : 0,
    hqIngredients: Array.isArray(hqIngredients) ? hqIngredients.flatMap(storedHqIngredient) : NO_HQ,
  };
}

/** A stored craft's states follow the rules of the engine build that worked them out, so only that build resumes them. */
export function storedCraftResumes(stored: { engine?: unknown }, engine: string): boolean {
  return stored.engine === engine;
}

// Item ids and counts cross into the engine as 32-bit integers too.
const MAX_INT32 = 0x7fffffff;

function isWholeNumberIn(value: unknown, min: number, max: number): value is number {
  return Number.isInteger(value) && (value as number) >= min && (value as number) <= max;
}

function storedConsumables(value: unknown): ConsumableSelection {
  if (typeof value !== "object" || value === null) return NO_CONSUMABLES;
  const { meal, medicine } = value as Record<string, unknown>;
  const selection = { meal: storedChoice(meal), medicine: storedChoice(medicine) };
  return selection.meal || selection.medicine ? selection : NO_CONSUMABLES;
}

function storedChoice(value: unknown): ConsumableChoice | null {
  if (typeof value !== "object" || value === null) return null;
  const { itemId, hq } = value as Record<string, unknown>;
  return isWholeNumberIn(itemId, 1, MAX_INT32) && typeof hq === "boolean" ? { itemId, hq } : null;
}

function storedHqIngredient(value: unknown): HQIngredientInput[] {
  if (typeof value !== "object" || value === null) return [];
  const { itemId, hqQuantity } = value as Record<string, unknown>;
  return isWholeNumberIn(itemId, 1, MAX_INT32) && isWholeNumberIn(hqQuantity, 0, MAX_INT32)
    ? [{ itemId, hqQuantity }]
    : [];
}

/** The manual starting quality and the HQ materials, each kept with the recipe it was set for. */
export interface StartingQualityInputs {
  /** A null recipe is a link's value, taken by the next recipe set. */
  manual: { recipeId: number | null; value: number };
  hq: { recipeId: number | null; ingredients: HQIngredientInput[] };
}

export const NO_STARTING_QUALITY: StartingQualityInputs = {
  manual: { recipeId: null, value: 0 },
  hq: { recipeId: null, ingredients: NO_HQ },
};

/** A setup replaces both inputs, so HQ materials left from an earlier craft never override a link's starting quality. */
export function inputsFromSetup(
  recipeId: number | null,
  setup: SimulatorSetup | undefined,
  recipeQuality = Number.POSITIVE_INFINITY,
): StartingQualityInputs {
  return {
    manual: { recipeId, value: Math.min(Math.max(0, setup?.startingQuality ?? 0), recipeQuality) },
    hq: { recipeId, ingredients: setup?.hqIngredients?.length ? setup.hqIngredients : NO_HQ },
  };
}

/** A manual value belongs to the recipe it was set for: another recipe's value resets to 0, a link's value is kept. */
export function inputsForRecipe(
  inputs: StartingQualityInputs,
  recipeId: number,
  recipeQuality: number,
): StartingQualityInputs {
  const { manual } = inputs;
  const value =
    manual.recipeId === null || manual.recipeId === recipeId ? Math.min(manual.value, recipeQuality) : 0;
  return { ...inputs, manual: { recipeId, value } };
}

/** "pending" until the HQ materials' quality answers for the current picks, "failed" when it could not be worked out. */
export type StartingQualityStatus = "ready" | "pending" | "failed";

/** HQ materials set the start while picked for this recipe; the start reads 0 until `hqQuality` answers for those picks (null: failed). */
export function startingQualityOf(
  recipeId: number | null,
  inputs: StartingQualityInputs,
  hqQuality: { ingredients: HQIngredientInput[]; value: number | null } | null,
): {
  hqIngredients: HQIngredientInput[];
  manualStartingQuality: number;
  startingQuality: number;
  startingQualityStatus: StartingQualityStatus;
} {
  const hqIngredients = recipeId !== null && inputs.hq.recipeId === recipeId ? inputs.hq.ingredients : NO_HQ;
  const { manual } = inputs;
  const manualStartingQuality = manual.recipeId === null || manual.recipeId === recipeId ? manual.value : 0;
  if (hqIngredients.length === 0) {
    return { hqIngredients, manualStartingQuality, startingQuality: manualStartingQuality, startingQualityStatus: "ready" };
  }
  const answered = hqQuality?.ingredients === hqIngredients ? hqQuality.value : undefined;
  return {
    hqIngredients,
    manualStartingQuality,
    startingQuality: answered ?? 0,
    startingQualityStatus: answered === undefined ? "pending" : answered === null ? "failed" : "ready",
  };
}
