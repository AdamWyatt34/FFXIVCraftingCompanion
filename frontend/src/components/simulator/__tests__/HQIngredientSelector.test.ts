import { createElement, isValidElement, type ReactElement, type ReactNode } from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it, vi } from "vitest";
import type { RecipeDto, RecipeIngredientDto } from "@/types/api";
import type { HQIngredientInput } from "@/types/simulator";
import { HQIngredientSelector } from "../HQIngredientSelector";

// next/image needs the Next runtime; the icon is not what these tests look at.
vi.mock("next/image", () => ({
  default: ({ src }: { src: string }) => createElement("img", { src }),
}));

function ingredient(itemId: number, itemName: string, quantity: number, canBeHQ: boolean): RecipeIngredientDto {
  return { itemId, itemName, iconUrl: `https://icons/${itemId}.png`, quantity, canBeHQ };
}

const QUARTZ = 37830;
const INGOT = 37833;
const recipe: RecipeDto = {
  recipeId: 35423,
  resultItem: {
    itemId: 39700,
    name: "Indagator's Alembic",
    iconUrl: "",
    itemLevel: 640,
    category: "Craftable",
    canBeHQ: true,
    recipeId: 35423,
    isEquipment: false,
  },
  craftType: "ALC",
  recipeLevel: 620,
  difficulty: 5720,
  quality: 12900,
  durability: 70,
  requiredCraftsmanship: 3700,
  requiredControl: 0,
  yield: 1,
  ingredients: [
    ingredient(15, "Ice Cluster", 5, false),
    ingredient(QUARTZ, "Rutilated Quartz", 2, true),
    ingredient(INGOT, "Ilmenite Ingot", 3, true),
  ],
  materialSummary: null,
  equipmentInfo: null,
};

function row(value: HQIngredientInput[], name: string) {
  const markup = renderToStaticMarkup(createElement(HQIngredientSelector, { recipe, value, onChange: () => {} }))
    // React separates adjacent text nodes with empty comments.
    .replaceAll("<!-- -->", "");
  const start = markup.indexOf(`>${name}</span>`);
  const end = markup.indexOf("</button>", markup.indexOf(`Increase HQ count for ${name}`));
  const html = markup.slice(start, end);
  const decrease = html.match(new RegExp(`<button([^>]*)aria-label="Decrease HQ count for ${name}"`))?.[1] ?? "";
  const increase = html.match(new RegExp(`<button([^>]*)aria-label="Increase HQ count for ${name}"`))?.[1] ?? "";
  return {
    nq: html.match(/>(\d+) NQ</)?.[1],
    hq: html.match(/font-mono[^"]*">(\d+)</)?.[1],
    canDecrease: !decrease.includes('disabled=""'),
    canIncrease: !increase.includes('disabled=""'),
  };
}

/** The selector's element tree, rendered inside a component so its hooks have one to run in. */
function tree(value: HQIngredientInput[], onChange: (hqIngredients: HQIngredientInput[]) => void): ReactNode {
  let rendered: ReactNode = null;
  function Capture() {
    rendered = HQIngredientSelector({ recipe, value, onChange });
    return rendered;
  }
  renderToStaticMarkup(createElement(Capture));
  return rendered;
}

function find(node: ReactNode, match: (props: Record<string, unknown>) => boolean): ReactElement<Record<string, unknown>> | null {
  if (Array.isArray(node)) {
    for (const child of node) {
      const found = find(child, match);
      if (found) return found;
    }
    return null;
  }
  if (!isValidElement<Record<string, unknown>>(node)) return null;
  if (match(node.props)) return node;
  return find(node.props.children as ReactNode, match);
}

const rowOf = (itemId: number) => (props: Record<string, unknown>) =>
  (props.ingredient as RecipeIngredientDto | undefined)?.itemId === itemId;

describe("HQIngredientSelector", () => {
  it("shows the simulator's HQ picks as its counts", () => {
    const value = [{ itemId: QUARTZ, hqQuantity: 2 }];
    expect(row(value, "Rutilated Quartz")).toEqual({ nq: "0", hq: "2", canDecrease: true, canIncrease: false });
    expect(row(value, "Ilmenite Ingot")).toEqual({ nq: "3", hq: "0", canDecrease: false, canIncrease: true });
  });

  it("shows nothing picked for an empty selection", () => {
    expect(row([], "Rutilated Quartz")).toEqual({ nq: "2", hq: "0", canDecrease: false, canIncrease: true });
  });

  it("adds one HQ to the picks it was given instead of replacing them", () => {
    const onChange = vi.fn();
    const rendered = tree([{ itemId: QUARTZ, hqQuantity: 1 }], onChange);

    (find(rendered, rowOf(INGOT))!.props.onIncrement as () => void)();
    expect(onChange).toHaveBeenLastCalledWith([
      { itemId: QUARTZ, hqQuantity: 1 },
      { itemId: INGOT, hqQuantity: 1 },
    ]);

    (find(rendered, rowOf(QUARTZ))!.props.onDecrement as () => void)();
    expect(onChange).toHaveBeenLastCalledWith([]);
  });
});
