import { expect, test } from "@playwright/test";

/**
 * Signed out, the site is served from the static data set and the WASM engine: search, recipe,
 * journey, plan, gear and simulator pages make no request to the API at all. e2e/mock-api.mjs
 * records every /api/ request it receives.
 */

const RECIPE_ID = 37783;
const RECIPE_NAME = "Pure Cosmic Tool-compatible Gel";
const RECIPE_SLUG = `${RECIPE_ID}-pure-cosmic-tool-compatible-gel`;
const MOCK_API = process.env.MOCK_API_URL ?? "http://localhost:3101";

test("signed-out pages never call the API", async ({ page, request }) => {
  await request.get(`${MOCK_API}/__reset`);

  await page.goto("/recipes?q=Cosmic");
  await expect(page.getByText(RECIPE_NAME).first()).toBeVisible();

  await page.goto(`/recipes/${RECIPE_SLUG}`);
  await expect(page.getByRole("heading", { name: RECIPE_NAME }).first()).toBeVisible();

  // What a crawler gets: the recipe is in the HTML itself, not fetched by the page's JavaScript.
  const html = await (await request.get(`/recipes/${RECIPE_SLUG}`)).text();
  expect(html).toContain(`<h1 class="text-xl font-bold text-text-primary">${RECIPE_NAME}</h1>`);
  expect(html).toContain("Oizys Rarities");
  expect(html).toMatch(new RegExp(`<link rel="canonical" href="[^"]*/recipes/${RECIPE_SLUG}"`));
  expect(html).toContain('"@type":"BreadcrumbList"');

  // A bare id or a stale name is a 308 to the canonical slug; junk is a real 404.
  for (const legacy of [`/recipes/${RECIPE_ID}`, `/recipes/${RECIPE_ID}-old-name`]) {
    const response = await request.get(legacy, { maxRedirects: 0 });
    expect(response.status(), legacy).toBe(308);
    expect(response.headers()["location"], legacy).toContain(`/recipes/${RECIPE_SLUG}`);
  }
  for (const junk of ["/recipes/999999999", "/recipes/not-a-recipe", `/recipes/${RECIPE_ID}-Pure`]) {
    expect((await request.get(junk)).status(), junk).toBe(404);
  }

  await page.goto(`/recipes/${RECIPE_SLUG}/journey`);
  await expect(page.getByRole("heading", { name: RECIPE_NAME }).first()).toBeVisible();
  const legacyJourney = await request.get(`/recipes/${RECIPE_ID}/journey`, { maxRedirects: 0 });
  expect(legacyJourney.status()).toBe(308);
  expect(legacyJourney.headers()["location"]).toContain(`/recipes/${RECIPE_SLUG}/journey`);

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
