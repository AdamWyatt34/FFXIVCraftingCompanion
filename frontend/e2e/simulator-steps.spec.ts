import { expect, test } from "@playwright/test";

/**
 * Each executed action builds on the state left by the previous one, whichever button is
 * clicked. Regression for the memoized action buttons keeping the execute handler (and with it
 * the crafting state) from their first render: a button whose availability never changed
 * would replay its action on the initial state.
 */

const RECIPE_ID = 37783;
const RECIPE_NAME = "Pure Cosmic Tool-compatible Gel";
const SIMULATOR_URL = `/simulator?recipeId=${RECIPE_ID}&craftsmanship=5000&control=5000&cp=700&level=100`;
const MOCK_API = process.env.MOCK_API_URL ?? "http://localhost:3101";

test("a second action continues from the first one's state", async ({
  page,
  request,
}) => {
  await request.get(`${MOCK_API}/__reset`);
  await page.goto(SIMULATOR_URL);
  await expect(
    page.getByRole("heading", { name: RECIPE_NAME }).first(),
  ).toBeVisible();
  await page.getByRole("button", { name: "Start Manual Craft" }).click();
  await expect(page.getByText("Step 0")).toBeVisible();

  const progress = page.getByRole("progressbar", { name: "Progress" });
  const cp = page.getByRole("progressbar", { name: "CP" });

  // Muscle Memory is only available on the first step, so its button re-renders afterwards;
  // Observe stays available throughout, so its button keeps whatever handler it first got.
  await page
    .getByRole("group", { name: "Progress actions" })
    .getByRole("button", { name: /^Muscle Memory/ })
    .click();
  await expect(page.getByText("Step 1")).toBeVisible();
  const progressAfterFirst = await progress.getAttribute("aria-valuenow");
  expect(Number(progressAfterFirst)).toBeGreaterThan(0);

  await page
    .getByRole("group", { name: "Buffs actions" })
    .getByRole("button", { name: /^Observe/ })
    .click();
  await expect(page.getByText("Step 2")).toBeVisible();

  await expect(progress).toHaveAttribute("aria-valuenow", progressAfterFirst!);
  await expect(cp).toHaveAttribute("aria-valuenow", String(700 - 6 - 7));
  await expect(
    page.getByRole("list", { name: "Step history" }).getByRole("button"),
  ).toHaveCount(2);
});
