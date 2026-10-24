import { expect, test } from "@playwright/test";

/**
 * With the WASM engine on and a recipe already cached, the simulator and the solver work with
 * the network disabled. The API is never reached for crafting math or game data: the recipe and
 * its crafting parameters come from the static data set under /data/v/ (e2e/fixtures/data) and
 * are cached by the service worker; e2e/mock-api.mjs stands in for the API's other endpoints.
 */

const RECIPE_ID = 37783;
const RECIPE_NAME = "Pure Cosmic Tool-compatible Gel";
const SIMULATOR_URL = `/simulator?recipeId=${RECIPE_ID}&craftsmanship=5000&control=5000&cp=700&level=100`;
const MOCK_API = process.env.MOCK_API_URL ?? "http://localhost:3101";

test.beforeEach(async ({ request }) => {
  await request.get(`${MOCK_API}/__reset`);
});

test("simulator and solver run offline from the WASM engine", async ({
  page,
  context,
  request,
}) => {
  // Online: install the service worker (it precaches the WASM engine and the worker chunk), then
  // reload once under its control so the page and the recipe responses land in its caches.
  await page.goto(SIMULATOR_URL);
  await expect(
    page.getByRole("heading", { name: RECIPE_NAME }).first(),
  ).toBeVisible();
  await page.evaluate(() => navigator.serviceWorker.ready);
  await page.reload();
  await expect(
    page.getByRole("heading", { name: RECIPE_NAME }).first(),
  ).toBeVisible();
  await expect
    .poll(async () =>
      page.evaluate(async () => {
        const keys = await (await caches.open("game-data")).keys();
        return keys.some((k) => k.url.includes("/data/v/") && k.url.includes("/recipes/"));
      }),
    )
    .toBe(true);

  // Offline: the mock API drops connections and the browser context is offline.
  await request.get(`${MOCK_API}/__offline?on=1`);
  await context.setOffline(true);
  await page.reload();
  await expect(
    page.getByRole("heading", { name: RECIPE_NAME }).first(),
  ).toBeVisible();

  // Step-by-step simulator: initial state, execute action, availability, all local.
  await page.getByRole("button", { name: "Start Manual Craft" }).click();
  await expect(page.getByText("Step 0")).toBeVisible();
  await page
    .getByRole("group", { name: "Progress actions" })
    .getByRole("button", { name: /^Basic Synthesis/ })
    .click();
  await expect(page.getByText("Step 1")).toBeVisible();
  await expect(
    page.getByRole("progressbar", { name: "Progress" }),
  ).not.toHaveAttribute("aria-valuenow", "0");

  // Solver: greedy preview and the MCTS search, both in the worker.
  await page.getByRole("button", { name: "Back to setup" }).click();
  await page
    .getByRole("button", { name: "Auto-solve crafting rotation" })
    .click();
  await expect(page.getByText("Rotation found", { exact: true })).toBeVisible({
    timeout: 30_000,
  });
  await page.getByRole("button", { name: "View Rotation" }).click();
  const result = page.getByRole("region", { name: "Solver result" });
  await expect(result).toBeVisible();
  await expect(result.getByText("MCTS", { exact: true })).toBeVisible();

  // Macro export is local too.
  await result.getByRole("button", { name: "Export Macro" }).click();
  await expect(page.getByText(/\/ac "/).first()).toBeVisible();

  const seen: string[] = await (
    await request.get(`${MOCK_API}/__requests`)
  ).json();
  expect(
    seen.filter((r) => r.includes("/api/crafting")),
    "no request reached /api/crafting",
  ).toEqual([]);
});
