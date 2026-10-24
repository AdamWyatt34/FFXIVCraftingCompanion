import { createElement, isValidElement, type ReactElement, type ReactNode } from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it, vi } from "vitest";
import type { CraftingConsumableDto } from "@/types/api";
import type { ConsumableSelection, CrafterStatsInput } from "@/types/simulator";
import { ConsumableSelect } from "../ConsumableSelect";

// next/image needs the Next runtime; the icon is not what these tests look at.
vi.mock("@/components/ui/ItemIcon", () => ({
  ItemIcon: ({ iconUrl }: { iconUrl: string }) => createElement("img", { "data-icon": iconUrl }),
}));

function consumable(
  itemId: number,
  name: string,
  itemLevel: number,
  kind: CraftingConsumableDto["kind"],
  canBeHQ: boolean,
  bonuses: CraftingConsumableDto["bonuses"],
): CraftingConsumableDto {
  return { itemId, name, iconUrl: `https://icons/${itemId}.png`, itemLevel, canBeHQ, kind, bonuses };
}

const steak = consumable(44091, "Rroneek Steak", 710, "Meal", true, [
  { stat: "Control", isRelative: true, value: 4, max: 77, valueHQ: 5, maxHQ: 97 },
  { stat: "CP", isRelative: true, value: 21, max: 73, valueHQ: 26, maxHQ: 92 },
]);
const tea = consumable(44092, "Rroneek Tea", 700, "Meal", true, [
  { stat: "Craftsmanship", isRelative: true, value: 4, max: 80, valueHQ: 5, maxHQ: 100 },
]);
const egg = consumable(4666, "Boiled Egg", 15, "Meal", true, [
  { stat: "Control", isRelative: true, value: 8, max: 4, valueHQ: 10, maxHQ: 5 },
]);
const draught = consumable(44170, "Craftsman's Draught", 720, "Medicine", false, [
  { stat: "CP", isRelative: false, value: 21, max: 0, valueHQ: 21, maxHQ: 0 },
]);
const tincture = consumable(27960, "Cunning Craftsman's Tincture", 400, "Medicine", true, [
  { stat: "Craftsmanship", isRelative: true, value: 3, max: 60, valueHQ: 4, maxHQ: 75 },
]);
const options = [steak, tea, egg, draught, tincture];
const baseStats: CrafterStatsInput = { level: 100, craftsmanship: 4900, control: 4800, cp: 620 };
const none: ConsumableSelection = { meal: null, medicine: null };

function render(selection: ConsumableSelection, overrides: Partial<Parameters<typeof ConsumableSelect>[0]> = {}): string {
  return renderToStaticMarkup(
    createElement(ConsumableSelect, { options, selection, baseStats, onChange: () => {}, ...overrides }),
  );
}

function selectOptions(markup: string, id: string): string[] {
  const select = markup.match(new RegExp(`<select id="${id}"[^>]*>(.*?)</select>`))?.[1] ?? "";
  return [...select.matchAll(/<option[^>]*>(.*?)<\/option>/g)].map((m) => m[1]);
}

function checkbox(markup: string, id: string): string {
  return markup.match(new RegExp(`<input id="${id}"[^>]*/>`))?.[0] ?? "";
}

function findById(node: ReactNode, id: string): ReactElement<Record<string, unknown>> | null {
  if (Array.isArray(node)) {
    for (const child of node) {
      const found = findById(child, id);
      if (found) return found;
    }
    return null;
  }
  if (!isValidElement<Record<string, unknown>>(node)) return null;
  if (node.props.id === id) return node;
  return findById(node.props.children as ReactNode, id);
}

describe("ConsumableSelect", () => {
  it("renders nothing for a data set without consumables", () => {
    expect(render(none, { options: [] })).toBe("");
  });

  it("lists each kind under its own select, in the catalogue's item-level order", () => {
    const markup = render(none);
    expect(markup).toContain('id="consumables"');
    expect(markup).toContain("Food and medicine (optional)");
    expect(selectOptions(markup, "consumables-meal")).toEqual([
      "None",
      "Rroneek Steak (i710)",
      "Rroneek Tea (i700)",
      "Boiled Egg (i15)",
    ]);
    expect(selectOptions(markup, "consumables-medicine")).toEqual([
      "None",
      "Craftsman&#x27;s Draught (i720)",
      "Cunning Craftsman&#x27;s Tincture (i400)",
    ]);
    expect(markup).toContain('<label for="consumables-meal"');
    expect(markup).toContain('<label for="consumables-medicine"');
  });

  it("disables HQ for an item that has no HQ, and for an empty slot", () => {
    const markup = render({ meal: null, medicine: { itemId: 44170, hq: false } });
    expect(checkbox(markup, "consumables-medicine-hq")).toContain('disabled=""');
    expect(checkbox(markup, "consumables-meal-hq")).toContain('disabled=""');
    expect(markup).toContain("+21 CP");
  });

  it("shows the boost and the effective stats for an HQ Rroneek Steak", () => {
    const markup = render({ meal: { itemId: 44091, hq: true }, medicine: null });
    expect(checkbox(markup, "consumables-meal-hq")).toContain('checked=""');
    expect(checkbox(markup, "consumables-meal-hq")).not.toContain('disabled=""');
    expect(markup).toContain("+97 Control (capped)");
    expect(markup).toContain("+92 CP");
    expect(markup).toContain('title="5% of Control, max 97"');
    expect(markup).toContain("Craftsmanship 4900 · Control 4800 → 4897 (+97) · CP 620 → 712 (+92)");
  });

  it("adds meal and medicine off the same base in the effective line", () => {
    const markup = render({ meal: { itemId: 44092, hq: false }, medicine: { itemId: 27960, hq: true } });
    expect(markup).toContain("Craftsmanship 4900 → 5055 (+155) · Control 4800 · CP 620");
  });

  it("shows the percentages when no stats are entered yet", () => {
    const markup = render({ meal: { itemId: 44091, hq: false }, medicine: null }, { baseStats: null });
    expect(markup).toContain("+4% Control (max 77)");
    expect(markup).not.toContain("→");
  });

  it("reports a pick, an HQ toggle and a reset to None as the new selection", () => {
    const onChange = vi.fn();
    const props = { options, baseStats, onChange };
    const medicine = { itemId: 44170, hq: false };

    const empty = ConsumableSelect({ ...props, selection: { meal: null, medicine } });
    (findById(empty, "consumables-meal")!.props.onChange as (e: unknown) => void)({ target: { value: "44091" } });
    expect(onChange).toHaveBeenLastCalledWith({ meal: { itemId: 44091, hq: true }, medicine });

    const picked = ConsumableSelect({ ...props, selection: { meal: { itemId: 44091, hq: true }, medicine } });
    (findById(picked, "consumables-meal-hq")!.props.onChange as () => void)();
    expect(onChange).toHaveBeenLastCalledWith({ meal: { itemId: 44091, hq: false }, medicine });

    (findById(picked, "consumables-meal")!.props.onChange as (e: unknown) => void)({ target: { value: "" } });
    expect(onChange).toHaveBeenLastCalledWith({ meal: null, medicine });
  });
});
