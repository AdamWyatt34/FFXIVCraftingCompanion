import { ApiClientError } from "@/lib/api-errors";
import type { ConsumableBonusDto, ConsumableKind, CraftingConsumableDto } from "@/types/api";
import type { ConsumableChoice, ConsumableSelection, CrafterStatsInput } from "@/types/simulator";
import { loadJson } from "./fetch";
import { CONSUMABLES_PATH } from "./paths";

// Port of FFXIV.Domain/Utilities/ConsumableStats.cs: crafting food and medicine on top of the
// stats the player entered. Pinned by goldens/consumable-stats.json.

export const NO_CONSUMABLES: ConsumableSelection = { meal: null, medicine: null };

export interface ConsumableStatBonuses {
  craftsmanship: number;
  control: number;
  cp: number;
}

/** A chosen meal holds the craft until the catalogue answers, so a stalled request must end as a failure. */
export const CONSUMABLES_TIMEOUT_MS = 10_000;

/** Meals first, each kind by item level descending; empty for a data set without the file. Rejects with a TimeoutError past CONSUMABLES_TIMEOUT_MS. */
export async function getCraftingConsumables(): Promise<CraftingConsumableDto[]> {
  const timeout = new AbortController();
  const timer = setTimeout(
    () => timeout.abort(new DOMException("consumables.json timed out", "TimeoutError")),
    CONSUMABLES_TIMEOUT_MS,
  );
  try {
    return await loadJson<CraftingConsumableDto[]>(CONSUMABLES_PATH, timeout.signal);
  } catch (err) {
    if (err instanceof ApiClientError && err.status === 404) return [];
    throw err;
  } finally {
    clearTimeout(timer);
  }
}

// The integer product comes before the division, as in the C# reference; a float percentage
// (Teamcraft's form) can floor one lower.
export function consumableBonus(base: number, bonus: ConsumableBonusDto, hq: boolean): number {
  const value = hq ? bonus.valueHQ : bonus.value;
  const max = hq ? bonus.maxHQ : bonus.max;
  return bonus.isRelative ? Math.min(max, Math.floor((base * value) / 100)) : value;
}

export function consumableStatBonuses(
  baseStats: CrafterStatsInput,
  consumable: CraftingConsumableDto,
  hq: boolean,
): ConsumableStatBonuses {
  const result: ConsumableStatBonuses = { craftsmanship: 0, control: 0, cp: 0 };
  for (const bonus of consumable.bonuses) {
    if (bonus.stat === "Craftsmanship") result.craftsmanship += consumableBonus(baseStats.craftsmanship, bonus, hq);
    else if (bonus.stat === "Control") result.control += consumableBonus(baseStats.control, bonus, hq);
    else if (bonus.stat === "CP") result.cp += consumableBonus(baseStats.cp, bonus, hq);
  }
  return result;
}

export function findConsumable(
  options: readonly CraftingConsumableDto[],
  choice: ConsumableChoice | null,
  kind: ConsumableKind,
): CraftingConsumableDto | undefined {
  return choice ? options.find((o) => o.itemId === choice.itemId && o.kind === kind) : undefined;
}

/** Meal and medicine each scale off the base stat and cap on their own, so neither sees the other's boost; a choice the options lack adds nothing. */
export function applyConsumables(
  baseStats: CrafterStatsInput,
  selection: ConsumableSelection,
  options: readonly CraftingConsumableDto[],
): CrafterStatsInput {
  let { craftsmanship, control, cp } = baseStats;
  for (const [choice, kind] of [[selection.meal, "Meal"], [selection.medicine, "Medicine"]] as const) {
    const consumable = findConsumable(options, choice, kind);
    if (!choice || !consumable) continue;
    const bonus = consumableStatBonuses(baseStats, consumable, choice.hq);
    craftsmanship += bonus.craftsmanship;
    control += bonus.control;
    cp += bonus.cp;
  }
  return { ...baseStats, craftsmanship, control, cp };
}

/** Drops choices the catalogue cannot honour (an item it lacks, the other kind, or HQ on an item with no HQ); `dropped` lists their item ids. */
export function sanitizeSelection(
  selection: ConsumableSelection,
  options: readonly CraftingConsumableDto[],
): { selection: ConsumableSelection; dropped: number[] } {
  const dropped: number[] = [];
  const keep = (choice: ConsumableChoice | null, kind: ConsumableKind): ConsumableChoice | null => {
    if (!choice) return null;
    const consumable = findConsumable(options, choice, kind);
    if (consumable && (!choice.hq || consumable.canBeHQ)) return choice;
    dropped.push(choice.itemId);
    return null;
  };
  const meal = keep(selection.meal, "Meal");
  const medicine = keep(selection.medicine, "Medicine");
  return dropped.length === 0 ? { selection, dropped } : { selection: { meal, medicine }, dropped };
}
