import { expect, test } from "@playwright/test";

/**
 * Signed out, the site is served from the static data set and the WASM engine: search, recipe,
 * journey, plan, gear and simulator pages make no request to the API at all. e2e/mock-api.mjs
 * records every /api/ request it receives.
 */

const RECIPE_ID = 37783;
const RECIPE_NAME = "Pure Cosmic Tool-compatible Gel";
const MOCK_API = process.env.MOCK_API_URL ?? "http://localhost:3101";

test("signed-out pages never call the API", async ({ page, request }) => {
  await request.get(`${MOCK_API}/__reset`);

  await page.goto("/recipes?q=Cosmic");
  await expect(page.getByText(RECIPE_NAME).first()).toBeVisible();

  await page.goto(`/recipes/${RECIPE_ID}`);
  await expect(page.getByRole("heading", { name: RECIPE_NAME }).first()).toBeVisible();

  await page.goto(`/recipes/${RECIPE_ID}/journey`);
  await expect(page.getByRole("heading", { name: RECIPE_NAME }).first()).toBeVisible();

  await page.goto("/recipes/plan");
  await page.getByRole("textbox", { name: "Search items" }).fill("Cosmic");
  await page.getByRole("button", { name: RECIPE_NAME }).first().click();
  await page.getByRole("button", { name: "Resolve" }).click();
  await expect(page.getByRole("button", { name: "Shopping List" })).toBeVisible();

  await page.goto("/gear?job=CRP&level=100");
  await expect(page.getByText("Saw of Stars").first()).toBeVisible();

  await page.goto(`/simulator?recipeId=${RECIPE_ID}&craftsmanship=5000&control=5000&cp=700&level=100`);
  await expect(page.getByRole("heading", { name: RECIPE_NAME }).first()).toBeVisible();

  const seen: string[] = await (await request.get(`${MOCK_API}/__requests`)).json();
  expect(seen, "no request reached the API").toEqual([]);
});
