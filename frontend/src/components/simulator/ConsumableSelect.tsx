"use client";

import { ItemIcon } from "@/components/ui/ItemIcon";
import { consumableBonus, consumableStatBonuses, findConsumable } from "@/lib/static-data/consumables";
import type { ConsumableBonusDto, CraftingConsumableDto } from "@/types/api";
import type { ConsumableChoice, ConsumableSelection, CrafterStatsInput } from "@/types/simulator";

interface ConsumableSelectProps {
  /** The catalogue, each kind already ordered by item level descending. */
  options: CraftingConsumableDto[];
  selection: ConsumableSelection;
  /** The stats as entered; the boost is computed against them. */
  baseStats: CrafterStatsInput | null;
  onChange: (selection: ConsumableSelection) => void;
}

const SLOTS = [
  { key: "meal", kind: "Meal", label: "Meal" },
  { key: "medicine", kind: "Medicine", label: "Medicine" },
] as const;

const STAT_FIELDS = { Craftsmanship: "craftsmanship", Control: "control", CP: "cp" } as const;

/** Null for a bonus that adds nothing, such as an empty slot. */
function bonusText(bonus: ConsumableBonusDto, hq: boolean, baseStats: CrafterStatsInput | null): { text: string; title: string } | null {
  const value = hq ? bonus.valueHQ : bonus.value;
  const max = hq ? bonus.maxHQ : bonus.max;
  if (value === 0) return null;
  if (!bonus.isRelative) return { text: `+${value} ${bonus.stat}`, title: `+${value} ${bonus.stat}` };

  const title = `${value}% of ${bonus.stat}, max ${max}`;
  if (!baseStats) return { text: `+${value}% ${bonus.stat} (max ${max})`, title };
  const base = baseStats[STAT_FIELDS[bonus.stat]];
  const amount = consumableBonus(base, bonus, hq);
  if (amount === 0) return null;
  const capped = Math.floor((base * value) / 100) > max;
  return { text: `+${amount} ${bonus.stat}${capped ? " (capped)" : ""}`, title };
}

function effectiveLine(baseStats: CrafterStatsInput, chosen: { consumable: CraftingConsumableDto; hq: boolean }[]): string {
  const total = { craftsmanship: 0, control: 0, cp: 0 };
  for (const { consumable, hq } of chosen) {
    const bonus = consumableStatBonuses(baseStats, consumable, hq);
    total.craftsmanship += bonus.craftsmanship;
    total.control += bonus.control;
    total.cp += bonus.cp;
  }
  return (Object.entries(STAT_FIELDS) as [keyof typeof STAT_FIELDS, keyof typeof total][])
    .map(([label, field]) => {
      const base = baseStats[field];
      const added = total[field];
      return added === 0 ? `${label} ${base}` : `${label} ${base} → ${base + added} (+${added})`;
    })
    .join(" · ");
}

/** The meal and medicine picker; renders nothing without a catalogue. */
export function ConsumableSelect({ options, selection, baseStats, onChange }: ConsumableSelectProps) {
  if (options.length === 0) return null;

  const chosen: { consumable: CraftingConsumableDto; hq: boolean }[] = [];
  const rows = SLOTS.map((slot) => {
    const choice = selection[slot.key];
    const consumable = findConsumable(options, choice, slot.kind);
    const hq = consumable?.canBeHQ === true && choice?.hq === true;
    if (consumable) chosen.push({ consumable, hq });

    const selectId = `consumables-${slot.key}`;
    const choose = (value: string) => {
      const picked = options.find((o) => o.kind === slot.kind && String(o.itemId) === value);
      const next: ConsumableChoice | null = picked ? { itemId: picked.itemId, hq: picked.canBeHQ } : null;
      onChange({ ...selection, [slot.key]: next });
    };
    const toggleHq = () => {
      if (consumable && choice) onChange({ ...selection, [slot.key]: { itemId: choice.itemId, hq: !hq } });
    };
    const readout = (consumable?.bonuses ?? []).flatMap((bonus) => {
      const part = bonusText(bonus, hq, baseStats);
      return part ? [{ ...part, stat: bonus.stat }] : [];
    });

    return (
      <div key={slot.key}>
        <label htmlFor={selectId} className="block text-sm font-medium text-text-secondary mb-1">
          {slot.label}
        </label>
        <div className="flex items-center gap-3">
          <select
            id={selectId}
            value={consumable ? String(consumable.itemId) : ""}
            aria-describedby={consumable ? `${selectId}-bonus` : undefined}
            onChange={(e) => choose(e.target.value)}
            className="min-w-0 flex-1 bg-bg-elevated border border-border focus:border-border-focus focus:outline-none text-text-primary text-sm rounded-lg px-3 py-2 transition-colors"
          >
            <option value="">None</option>
            {options
              .filter((o) => o.kind === slot.kind)
              .map((o) => (
                <option key={o.itemId} value={o.itemId}>
                  {`${o.name} (i${o.itemLevel})`}
                </option>
              ))}
          </select>
          <div className="flex items-center gap-2 shrink-0">
            <input
              id={`${selectId}-hq`}
              type="checkbox"
              checked={hq}
              disabled={!consumable?.canBeHQ}
              aria-label={`${slot.label} HQ`}
              onChange={toggleHq}
              className="w-4 h-4 rounded border-border bg-bg-elevated accent-gold disabled:opacity-40"
            />
            <label htmlFor={`${selectId}-hq`} className="text-sm text-text-secondary">
              HQ
            </label>
          </div>
        </div>
        {consumable && (
          <div id={`${selectId}-bonus`} className="flex items-center gap-2 mt-1.5 text-xs text-text-secondary">
            <ItemIcon iconUrl={consumable.iconUrl} alt="" size="sm" />
            <span>
              {readout.map((part, i) => (
                <span key={part.stat} title={part.title}>
                  {i > 0 ? `, ${part.text}` : part.text}
                </span>
              ))}
            </span>
          </div>
        )}
      </div>
    );
  });

  return (
    <section id="consumables" aria-labelledby="consumables-heading" className="mt-4 pt-4 border-t border-border space-y-3">
      <div>
        <h3 id="consumables-heading" className="text-sm font-medium text-text-primary">
          Food and medicine (optional)
        </h3>
        <p className="text-xs text-text-muted mt-0.5">
          Enter your stats without food or medicine active; the boost is added here.
        </p>
      </div>
      {rows}
      {baseStats && chosen.length > 0 && <p className="text-xs text-text-secondary">{effectiveLine(baseStats, chosen)}</p>}
    </section>
  );
}
