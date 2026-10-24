import { expect, test, type Page } from "@playwright/test";

// Stats sit on recipe 35423's required 3700/3280 so solving stays enabled.

const RECIPE_NAME = "Indagator's Alembic";
const FIRST_MEAL = "All i Pebre (i750)";
const PICKER = "Food and medicine (optional)";
const HQ_MATERIAL = "Rutilated Quartz";
const CONSUMABLES = "**/data/v/**/consumables.json";
const UNAPPLIED = "Food and medicine couldn't load, so your selection isn't applied.";
const OTHER_RECIPE = "Pure Cosmic Tool-compatible Gel";
const CRAFT_CLOSED = "The craft in progress was closed because its food or medicine couldn't load.";
const ENGINE_UPDATED =
  "The craft in progress was closed because the crafting engine was updated; start it again from the same setup.";
const SESSION = "ffxiv_simulator_state";
const BELOW_REQUIRED = "Your craftsmanship 3000 is below this recipe's required 3700.";
// Muscle Memory then Basic Synthesis, packed as a solver link carries them.
const SOLVER_ROTATION = "_gGIGwGGoQ";

async function openRecipeWithStats(page: Page) {
  await page.goto("/simulator");
  await page.getByRole("textbox", { name: "Search items" }).fill("Indagator");
  await page.getByRole("button", { name: RECIPE_NAME }).first().click();
  await expect(page.getByRole("heading", { name: RECIPE_NAME }).first()).toBeVisible();

  await page.getByLabel("Level", { exact: true }).fill("90");
  await page.getByLabel("Craftsmanship", { exact: true }).fill("3700");
  await page.getByLabel("Control", { exact: true }).fill("3280");
  await page.getByLabel("CP", { exact: true }).fill("600");
  await expect(page.getByRole("heading", { name: "Recipe Parameters" })).toBeVisible();
}

async function recordSolveStats(page: Page) {
  await page.addInitScript(() => {
    const sent: unknown[] = [];
    (window as unknown as { __solveStats: unknown[] }).__solveStats = sent;
    const proto = Worker.prototype as unknown as { postMessage: (message: unknown, ...rest: unknown[]) => void };
    const post = proto.postMessage;
    proto.postMessage = function (this: Worker, message: unknown, ...rest: unknown[]) {
      const request = message as { type?: string; stats?: unknown } | null;
      if (request?.type === "solve") sent.push(request.stats);
      return post.call(this, message, ...rest);
    };
  });
  return () => page.evaluate(() => (window as unknown as { __solveStats: unknown[] }).__solveStats);
}

test("the meal picker boosts the stats Quick Solve runs on", async ({ page }) => {
  // The result card shows no stats, so the solve request the worker receives is the observable input.
  const solveStats = await recordSolveStats(page);
  await openRecipeWithStats(page);

  const picker = page.getByRole("region", { name: PICKER });
  const meal = picker.getByLabel("Meal", { exact: true });
  await expect(meal.locator("option").nth(1)).toHaveText(FIRST_MEAL);
  await meal.selectOption({ label: FIRST_MEAL });
  const hq = picker.getByRole("checkbox", { name: "Meal HQ" });
  await hq.check();
  await expect(hq).toBeChecked();
  await expect(
    picker.getByText("Craftsmanship 3700 · Control 3280 → 3395 (+115) · CP 600 → 700 (+100)", { exact: true }),
  ).toBeVisible();

  await expect(page.getByRole("button", { name: "Auto-solve crafting rotation" })).toBeEnabled();
  const quickSolve = page.getByRole("button", { name: /^Quick Solve/ });
  await expect(quickSolve).toBeEnabled();
  await quickSolve.click();
  await expect(page.getByText("Rotation found", { exact: true })).toBeVisible({ timeout: 30_000 });

  expect(await solveStats()).toEqual([{ level: 90, craftsmanship: 3700, control: 3395, cp: 700 }]);
});

test.describe("a craft started with a meal and an HQ material", () => {
  // Playwright cannot route a request the service worker answers.
  test.use({ serviceWorkers: "block" });

  test("keeps them through Back to setup and a reload", async ({ page }) => {
    await openRecipeWithStats(page);
    const picker = page.getByRole("region", { name: PICKER });
    const meal = picker.getByLabel("Meal", { exact: true });
    await meal.selectOption({ label: FIRST_MEAL });
    const mealId = Number(await meal.inputValue());
    await picker.getByRole("checkbox", { name: "Meal HQ" }).check();
    await page.getByRole("button", { name: `Increase HQ count for ${HQ_MATERIAL}` }).click();
    const fewerHq = page.getByRole("button", { name: `Decrease HQ count for ${HQ_MATERIAL}` });
    await expect(fewerHq).toBeEnabled();

    const startCraft = page.getByRole("button", { name: "Start Manual Craft" });
    const quality = page.getByRole("progressbar", { name: "Quality" });
    await startCraft.click();
    await expect(page.getByText("Step 0")).toBeVisible();
    const startingQuality = await quality.getAttribute("aria-valuenow");
    expect(Number(startingQuality)).toBeGreaterThan(0);

    await page.getByRole("button", { name: "Back to setup" }).click();
    await expect(fewerHq).toBeEnabled();
    await startCraft.click();
    await expect(quality).toHaveAttribute("aria-valuenow", startingQuality!);

    // The third touch's quality, worked out on the boosted control, is what the reloaded craft must reproduce.
    const basicTouch = page.getByRole("group", { name: "Quality actions" }).getByRole("button", { name: /^Basic Touch/ });
    for (const step of [1, 2, 3]) {
      await basicTouch.click();
      await expect(page.getByText(`Step ${step}`)).toBeVisible();
    }
    const thirdTouch = await quality.getAttribute("aria-valuenow");
    await page.getByRole("button", { name: "Undo Last" }).click();
    await expect(page.getByText("Step 2")).toBeVisible();

    let release!: () => void;
    const held = new Promise<void>((resolve) => (release = resolve));
    await page.route(CONSUMABLES, async (route) => {
      await held;
      await route.continue();
    });
    await page.reload();
    await expect(page.getByText("Step 2")).toBeVisible();
    await expect(basicTouch).toBeVisible();
    // Long enough for an availability check that did not wait for the catalogue to have enabled it.
    await page.waitForTimeout(1_000);
    await expect(basicTouch).toBeDisabled();
    release();
    await expect(basicTouch).toBeEnabled();
    await basicTouch.click();
    await expect(page.getByText("Step 3")).toBeVisible();
    await expect(quality).toHaveAttribute("aria-valuenow", thirdTouch!);

    await page.getByRole("button", { name: "Back to setup" }).click();
    await expect(fewerHq).toBeEnabled();
    await expect(page.getByText("from HQ materials")).toBeVisible();
    await startCraft.click();
    await expect(page.getByText("Step 0")).toBeVisible();

    // Its states were worked out on the boosted stats, so without the meal the craft closes instead of resuming.
    const failCatalogue = () =>
      page.route(CONSUMABLES, (route) => route.fulfill({ status: 500, contentType: "application/json", body: "{}" }));
    await page.unroute(CONSUMABLES);
    await failCatalogue();
    await page.reload();
    await expect(page.getByText(CRAFT_CLOSED, { exact: true })).toBeVisible();
    await expect(page.getByText(UNAPPLIED, { exact: true })).toBeVisible();
    await expect(page.getByLabel("Craftsmanship", { exact: true })).toHaveValue("3700");
    await expect(fewerHq).toBeEnabled();
    await expect(startCraft).toBeVisible();
    await expect(page.getByRole("group", { name: "Quality actions" })).toHaveCount(0);

    // A catalogue that answers without the meal, empty for a data set with no consumables.json or
    // missing it after a data refresh, cannot reproduce those states either.
    await page.unroute(CONSUMABLES);
    await page.route(CONSUMABLES, (route) =>
      route.fulfill({ status: 404, contentType: "application/json", body: '{"error":"not found"}' }),
    );
    await page.reload();
    await expect(page.getByText(CRAFT_CLOSED, { exact: true })).toBeVisible();
    await expect(startCraft).toBeVisible();
    await expect(page.getByRole("group", { name: "Quality actions" })).toHaveCount(0);

    await page.unroute(CONSUMABLES);
    await page.route(CONSUMABLES, async (route) => {
      const catalogue: { itemId: number }[] = await (await route.fetch()).json();
      await route.fulfill({ json: catalogue.filter((consumable) => consumable.itemId !== mealId) });
    });
    await page.reload();
    await expect(page.getByText(CRAFT_CLOSED, { exact: true })).toBeVisible();
    await expect(startCraft).toBeVisible();
    await expect(page.getByRole("group", { name: "Quality actions" })).toHaveCount(0);

    await page.unroute(CONSUMABLES);
    await page.reload();
    await expect(page.getByText("Step 0")).toBeVisible();
    await expect(basicTouch).toBeEnabled();

    // A closed reload must not auto-save the reduced selection over the stored craft.
    await page.getByRole("button", { name: "Back to setup" }).click();
    await expect(meal).toHaveValue(String(mealId));

    // A craft without food or medicine needs nothing from the catalogue, so it resumes.
    await meal.selectOption({ index: 0 });
    await startCraft.click();
    await basicTouch.click();
    await expect(page.getByText("Step 1")).toBeVisible();
    await failCatalogue();
    await page.reload();
    await expect(page.getByText("Step 1")).toBeVisible();
    await expect(basicTouch).toBeEnabled();
    await basicTouch.click();
    await expect(page.getByText("Step 2")).toBeVisible();
  });
});

test.describe("a craft run while food and medicine can't load", () => {
  // Playwright cannot route a request the service worker answers.
  test.use({ serviceWorkers: "block" });

  test("records no meal in its share link or its stored session", async ({ page, context }) => {
    await context.grantPermissions(["clipboard-read", "clipboard-write"]);
    const sharedLink = async () => {
      await page.evaluate(() => navigator.clipboard.writeText(""));
      await page.getByRole("button", { name: "Share rotation link" }).click();
      await expect.poll(() => page.evaluate(() => navigator.clipboard.readText())).toContain("recipeId=35423");
      return new URL(await page.evaluate(() => navigator.clipboard.readText()));
    };
    await openRecipeWithStats(page);
    const meal = page.getByRole("region", { name: PICKER }).getByLabel("Meal", { exact: true });
    await meal.selectOption({ label: FIRST_MEAL });
    const mealId = await meal.inputValue();
    const startCraft = page.getByRole("button", { name: "Start Manual Craft" });
    const basicSynthesis = page
      .getByRole("group", { name: "Progress actions" })
      .getByRole("button", { name: /^Basic Synthesis/ });
    await startCraft.click();
    await basicSynthesis.click();
    await expect(page.getByText("Step 1")).toBeVisible();
    const link = await sharedLink();
    expect(link.searchParams.get("meal")).toBe(mealId);

    await page.route(CONSUMABLES, (route) => route.fulfill({ status: 500, contentType: "application/json", body: "{}" }));
    await page.goto(`${link.pathname}${link.search}`);
    await expect(page.getByText("Step 1")).toBeVisible();
    await expect(page.getByText(UNAPPLIED, { exact: true })).toBeVisible();
    expect((await sharedLink()).searchParams.get("meal")).toBeNull();

    await page.getByRole("button", { name: "Back to setup" }).click();
    await startCraft.click();
    for (const step of [1, 2]) {
      await basicSynthesis.click();
      await expect(page.getByText(`Step ${step}`)).toBeVisible();
    }
    expect((await sharedLink()).searchParams.get("meal")).toBeNull();

    await page.unroute(CONSUMABLES);
    // A reload would open the link again; the plain page resumes the stored craft.
    await page.goto("/simulator");
    await expect(page.getByText("Step 2")).toBeVisible();
    await expect(basicSynthesis).toBeEnabled();
    await expect(page.getByText(CRAFT_CLOSED, { exact: true })).toHaveCount(0);
    expect((await sharedLink()).searchParams.get("meal")).toBeNull();
    await page.getByRole("button", { name: "Back to setup" }).click();
    await expect(meal).toHaveValue("");
  });
});

test("a stored craft from another engine build is closed and its setup kept", async ({ page }) => {
  await openRecipeWithStats(page);
  const meal = page.getByRole("region", { name: PICKER }).getByLabel("Meal", { exact: true });
  await meal.selectOption({ label: FIRST_MEAL });
  const mealId = await meal.inputValue();
  await page.getByRole("button", { name: `Increase HQ count for ${HQ_MATERIAL}` }).click();
  const fewerHq = page.getByRole("button", { name: `Decrease HQ count for ${HQ_MATERIAL}` });
  await expect(fewerHq).toBeEnabled();
  const startCraft = page.getByRole("button", { name: "Start Manual Craft" });
  const basicSynthesis = page
    .getByRole("group", { name: "Progress actions" })
    .getByRole("button", { name: /^Basic Synthesis/ });

  // null removes the field: a session that records no engine build.
  for (const engine of ["0000000000000000000000000000000000000000", null]) {
    await startCraft.click();
    await basicSynthesis.click();
    await expect(page.getByText("Step 1")).toBeVisible();
    await expect
      .poll(() => page.evaluate((key) => JSON.parse(sessionStorage.getItem(key) ?? "{}").stateHistory?.length, SESSION))
      .toBe(1);
    await page.evaluate(
      ([key, build]) => {
        const session = JSON.parse(sessionStorage.getItem(key)!);
        if (build === null) delete session.engine;
        else session.engine = build;
        sessionStorage.setItem(key, JSON.stringify(session));
      },
      [SESSION, engine] as const,
    );

    await page.reload();
    await expect(page.getByText(ENGINE_UPDATED, { exact: true })).toBeVisible();
    await expect(page.getByText(CRAFT_CLOSED, { exact: true })).toHaveCount(0);
    await expect(startCraft).toBeVisible();
    await expect(page.getByRole("group", { name: "Progress actions" })).toHaveCount(0);
    await expect(page.getByLabel("Craftsmanship", { exact: true })).toHaveValue("3700");
    await expect(meal).toHaveValue(mealId);
    await expect(fewerHq).toBeEnabled();
    expect(await page.evaluate((key) => sessionStorage.getItem(key), SESSION)).toBeNull();
  }
});

test.describe("a restored craft whose HQ materials' quality is still being worked out", () => {
  // Playwright cannot route a request the service worker answers.
  test.use({ serviceWorkers: "block" });

  test("shares the starting quality the craft ran from", async ({ page, context }) => {
    await context.grantPermissions(["clipboard-read", "clipboard-write"]);
    await openRecipeWithStats(page);
    await page.getByRole("button", { name: `Increase HQ count for ${HQ_MATERIAL}` }).click();
    await page.getByRole("button", { name: "Start Manual Craft" }).click();
    await expect(page.getByText("Step 0")).toBeVisible();
    const startingQuality = await page.getByRole("progressbar", { name: "Quality" }).getAttribute("aria-valuenow");
    expect(Number(startingQuality)).toBeGreaterThan(0);
    await page.getByRole("group", { name: "Progress actions" }).getByRole("button", { name: /^Basic Synthesis/ }).click();
    await expect(page.getByText("Step 1")).toBeVisible();

    // Held, the recipe's parameters keep the HQ materials' quality pending after the reload.
    let release!: () => void;
    const held = new Promise<void>((resolve) => (release = resolve));
    await page.route("**/data/v/**/recipes/354.json", async (route) => {
      await held;
      await route.continue();
    });
    await page.reload();
    await expect(page.getByText("Step 1")).toBeVisible();
    await page.getByRole("button", { name: "Share rotation link" }).click();
    await expect.poll(() => page.evaluate(() => navigator.clipboard.readText())).toContain("recipeId=35423");
    const shared = new URL(await page.evaluate(() => navigator.clipboard.readText()));
    expect(shared.searchParams.get("sq")).toBe(startingQuality);
    release();
  });
});

test("a solve link waits out a block and never solves past a required stat", async ({ page }) => {
  await page.goto("/simulator?recipeId=35423&craftsmanship=3000&control=3280&cp=600&level=90&solve=true");
  const autoSolve = page.getByRole("button", { name: "Auto-solve crafting rotation" });
  await expect(autoSolve).toBeDisabled();
  await expect(autoSolve).toHaveAccessibleDescription(BELOW_REQUIRED);
  // Long enough for a solve that did not wait to have started and failed.
  await page.waitForTimeout(1_000);
  await expect(autoSolve).toBeDisabled();
  await expect(page.getByText("Solve Failed")).toHaveCount(0);

  await page.getByLabel("Craftsmanship", { exact: true }).fill("3700");
  await expect(page.getByText("Rotation found", { exact: true })).toBeVisible({ timeout: 30_000 });
});

test("Re-solve starts no solve below a required stat", async ({ page }) => {
  await openRecipeWithStats(page);
  const quickSolve = page.getByRole("button", { name: /^Quick Solve/ });
  await quickSolve.click();
  const found = page.getByText("Rotation found", { exact: true });
  await expect(found).toBeVisible({ timeout: 30_000 });
  await page.getByLabel("Craftsmanship", { exact: true }).fill("3000");
  await expect(quickSolve).toBeDisabled();

  const refused = page.getByRole("alert").filter({ hasText: BELOW_REQUIRED });
  await page.getByRole("button", { name: "Re-solve with current settings" }).click();
  await expect(refused).toBeVisible();
  await expect(found).toBeVisible();
  await expect(page.getByText("Solve Failed")).toHaveCount(0);
  await expect(refused).toHaveCount(0);

  await page.getByRole("button", { name: "View Rotation" }).click();
  const result = page.getByRole("region", { name: "Solver result" });
  await result.getByRole("button", { name: "Re-Solve" }).click();
  await expect(refused).toBeVisible();
  await expect(result).toBeVisible();
  await expect(page.getByText("Solve Failed")).toHaveCount(0);
});

test("a solver result is shared with the setup it was solved on, not one edited after", async ({ page, context }) => {
  await context.grantPermissions(["clipboard-read", "clipboard-write"]);
  await openRecipeWithStats(page);
  const meal = page.getByRole("region", { name: PICKER }).getByLabel("Meal", { exact: true });
  await meal.selectOption({ label: FIRST_MEAL });
  const mealId = await meal.inputValue();
  const startingQuality = page.getByLabel("Starting Quality", { exact: true });
  await startingQuality.fill("500");

  await page.getByRole("button", { name: /^Quick Solve/ }).click();
  await expect(page.getByText("Rotation found", { exact: true })).toBeVisible({ timeout: 30_000 });
  await page.getByRole("button", { name: "View Rotation" }).click();
  const result = page.getByRole("region", { name: "Solver result" });
  await expect(result).toBeVisible();

  await startingQuality.fill("0");
  await meal.selectOption({ index: 0 });
  await result.getByRole("button", { name: "Share" }).click();
  await expect(result.getByText("Copied!")).toBeVisible();
  const shared = new URL(await page.evaluate(() => navigator.clipboard.readText()));
  expect(shared.searchParams.get("sq")).toBe("500");
  expect(shared.searchParams.get("meal")).toBe(mealId);
});

test("a share link opened over a stored craft loads the link's recipe, stats and rotation", async ({ page }) => {
  await openRecipeWithStats(page);
  await page.getByRole("button", { name: "Start Manual Craft" }).click();
  await expect(page.getByText("Step 0")).toBeVisible();

  const link = "/simulator?recipeId=37783&craftsmanship=5000&control=5000&cp=700&level=100";
  const linkedRecipe = page.getByRole("heading", { name: "Pure Cosmic Tool-compatible Gel" }).first();
  await page.goto(link);
  await expect(linkedRecipe).toBeVisible();
  await expect(page.getByLabel("Craftsmanship", { exact: true })).toHaveValue("5000");
  await expect(page.getByRole("button", { name: "Start Manual Craft" })).toBeVisible();
  await expect(page.getByRole("button", { name: "Back to setup" })).toHaveCount(0);

  await page.goto(`${link}&rotation=100379,100001`);
  await expect(page.getByText("Step 2")).toBeVisible();
  await expect(page.getByRole("list", { name: "Step history" }).getByRole("listitem")).toHaveCount(2);
  await expect(page.getByRole("progressbar", { name: "CP" })).toHaveAttribute("aria-valuemax", "700");
  await page.getByRole("button", { name: "Back to setup" }).click();
  await expect(linkedRecipe).toBeVisible();
});

test("what-if rows follow the starting quality Quick Solve uses", async ({ page }) => {
  await openRecipeWithStats(page);
  await page.getByRole("button", { name: "What If", exact: true }).click();
  const explorer = page.getByLabel("What If stat explorer");
  const startingQuality = page.getByLabel("Starting Quality", { exact: true });

  await explorer.getByRole("button", { name: "Current Gear" }).click();
  await expect(explorer.getByRole("row").filter({ hasText: "Current Gear" })).toContainText("%");
  await startingQuality.fill("10000");
  await expect(explorer.getByText(/^Drag a stat slider or click a preset/)).toBeVisible();

  await explorer.getByLabel("Adjust Control").fill("3300");
  const current = explorer.getByRole("row").filter({ has: page.getByRole("cell", { name: "Current", exact: true }) });
  await expect(current).toContainText("%");
  const fromHighStart = await current.textContent();
  await startingQuality.fill("0");
  await expect
    .poll(async () => {
      const text = (await current.textContent()) ?? "";
      return text.includes("%") && text !== fromHighStart;
    })
    .toBe(true);
});

test("what-if sliders follow a meal picked with the explorer open", async ({ page }) => {
  const solveStats = await recordSolveStats(page);
  await openRecipeWithStats(page);
  await page.getByRole("button", { name: "What If", exact: true }).click();
  const explorer = page.getByLabel("What If stat explorer");
  const cp = explorer.getByLabel("CP value");
  await expect(cp).toHaveValue("600");

  const picker = page.getByRole("region", { name: PICKER });
  await picker.getByLabel("Meal", { exact: true }).selectOption({ label: FIRST_MEAL });
  await picker.getByRole("checkbox", { name: "Meal HQ" }).check();
  await expect(cp).toHaveValue("700");

  await explorer.getByLabel("Adjust Control").fill("3405");
  const sweep = [-30, -20, -10, 0, 10, 20, 30].map((step) => ({
    level: 90,
    craftsmanship: 3700,
    control: 3405 + step,
    cp: 700,
  }));
  await expect
    .poll(async () => ((await solveStats()) as { control: number }[]).sort((a, b) => a.control - b.control))
    .toEqual(sweep);
  await expect(explorer.getByLabel("Control value")).toHaveValue("3405");
});

test("the what-if + button sweeps around the raised stat", async ({ page }) => {
  const solveStats = await recordSolveStats(page);
  await openRecipeWithStats(page);
  await page.getByRole("button", { name: "What If", exact: true }).click();
  const explorer = page.getByLabel("What If stat explorer");
  const cp = explorer.getByLabel("CP value");
  await expect(cp).toHaveValue("600");

  await explorer.getByRole("button", { name: "Increase CP by 10" }).click();
  const sweep = [-30, -20, -10, 0, 10, 20, 30].map((step) => ({
    level: 90,
    craftsmanship: 3700,
    control: 3280,
    cp: 610 + step,
  }));
  await expect
    .poll(async () => ((await solveStats()) as { cp: number }[]).sort((a, b) => a.cp - b.cp))
    .toEqual(sweep);
  await expect(cp).toHaveValue("610");
});

test("a value typed into a what-if stat box sweeps around it", async ({ page }) => {
  const solveStats = await recordSolveStats(page);
  await openRecipeWithStats(page);
  await page.getByRole("button", { name: "What If", exact: true }).click();
  const explorer = page.getByLabel("What If stat explorer");
  const control = explorer.getByLabel("Control value");
  await expect(control).toHaveValue("3280");

  await control.fill("3420");
  const sweep = [-30, -20, -10, 0, 10, 20, 30].map((step) => ({
    level: 90,
    craftsmanship: 3700,
    control: 3420 + step,
    cp: 600,
  }));
  await expect
    .poll(async () => ((await solveStats()) as { control: number }[]).sort((a, b) => a.control - b.control))
    .toEqual(sweep);
  await expect(control).toHaveValue("3420");
});

test("a solver result is dropped when the recipe changes", async ({ page }) => {
  await openRecipeWithStats(page);
  await page.getByRole("button", { name: /^Quick Solve/ }).click();
  await expect(page.getByText("Rotation found", { exact: true })).toBeVisible({ timeout: 30_000 });
  await page.getByRole("button", { name: "View Rotation" }).click();
  const result = page.getByRole("region", { name: "Solver result" });
  await expect(result).toBeVisible();

  await page.getByRole("button", { name: "Change", exact: true }).click();
  await page.getByRole("textbox", { name: "Search items" }).fill("Pure Cosmic");
  await page.getByRole("button", { name: OTHER_RECIPE }).first().click();
  await expect(page.getByRole("heading", { name: OTHER_RECIPE }).first()).toBeVisible();
  await expect(result).toHaveCount(0);
  await expect(page.getByText("Rotation found", { exact: true })).toHaveCount(0);
});

test("a manual craft's share link names the selected recipe, not the one the page opened on", async ({ page, context }) => {
  await context.grantPermissions(["clipboard-read", "clipboard-write"]);
  await page.goto("/simulator?recipeId=37783&craftsmanship=5000&control=5000&cp=700&level=100");
  await expect(page.getByRole("heading", { name: OTHER_RECIPE }).first()).toBeVisible();

  await page.getByRole("button", { name: "Change", exact: true }).click();
  await page.getByRole("textbox", { name: "Search items" }).fill("Indagator");
  await page.getByRole("button", { name: RECIPE_NAME }).first().click();
  await expect(page.getByRole("heading", { name: RECIPE_NAME }).first()).toBeVisible();
  await page.getByRole("button", { name: "Start Manual Craft" }).click();
  await page.getByRole("group", { name: "Progress actions" }).getByRole("button", { name: /^Basic Synthesis/ }).click();
  await expect(page.getByText("Step 1")).toBeVisible();

  await page.getByRole("button", { name: "Share rotation link" }).click();
  await expect.poll(() => page.evaluate(() => navigator.clipboard.readText())).toContain("recipeId=35423");
});

test("a plain link with an unusable recipe id is reported instead of thrown", async ({ page }) => {
  const errors: Error[] = [];
  page.on("pageerror", (error) => errors.push(error));

  await page.goto("/simulator?recipeId=-5");
  await expect(page.getByText("The recipe in this link is invalid and was ignored.")).toBeVisible();
  await page.goto("/simulator?recipeId=999999");
  await expect(page.getByText("The recipe in this link could not be loaded.")).toBeVisible();
  expect(errors).toEqual([]);
});

test.describe("a plain link's recipe", () => {
  // Playwright cannot route a request the service worker answers.
  test.use({ serviceWorkers: "block" });

  test("that loads after another pick leaves the pick in place", async ({ page }) => {
    let release!: () => void;
    const held = new Promise<void>((resolve) => (release = resolve));
    await page.route("**/data/v/**/recipes/377.json", async (route) => {
      await held;
      await route.continue();
    });
    await page.goto("/simulator?recipeId=37783");
    await page.getByRole("textbox", { name: "Search items" }).fill("Indagator");
    await page.getByRole("button", { name: RECIPE_NAME }).first().click();
    await expect(page.getByRole("heading", { name: RECIPE_NAME }).first()).toBeVisible();

    const linked = page.waitForResponse("**/data/v/**/recipes/377.json");
    release();
    await linked;
    // Settle time: a late link recipe would replace the pick within this window.
    await page.waitForTimeout(500);
    await expect(page.getByRole("heading", { name: RECIPE_NAME }).first()).toBeVisible();
    await expect(page.getByRole("heading", { name: OTHER_RECIPE })).toHaveCount(0);
  });
});

test.describe("a solver link's recipe", () => {
  // Playwright cannot route a request the service worker answers.
  test.use({ serviceWorkers: "block" });

  test("that loads after another pick leaves the pick in place and solvable", async ({ page }) => {
    let release!: () => void;
    const held = new Promise<void>((resolve) => (release = resolve));
    await page.route("**/data/v/**/recipes/377.json", async (route) => {
      await held;
      await route.continue();
    });
    await page.goto(`/simulator?solve=1&recipeId=37783&c=5000&ct=5000&cp=700&lv=100&r=${SOLVER_ROTATION}`);
    await page.getByRole("textbox", { name: "Search items" }).fill("Indagator");
    await page.getByRole("button", { name: RECIPE_NAME }).first().click();
    await expect(page.getByRole("heading", { name: RECIPE_NAME }).first()).toBeVisible();

    const linked = page.waitForResponse("**/data/v/**/recipes/377.json");
    release();
    await linked;
    // Settle time: a late link recipe would replace the pick within this window.
    await page.waitForTimeout(500);
    await expect(page.getByRole("heading", { name: RECIPE_NAME }).first()).toBeVisible();
    await expect(page.getByRole("heading", { name: OTHER_RECIPE })).toHaveCount(0);
    await expect(page.getByRole("button", { name: "Back to setup" })).toHaveCount(0);

    await page.getByLabel("Level", { exact: true }).fill("90");
    await page.getByLabel("Craftsmanship", { exact: true }).fill("3700");
    await page.getByLabel("Control", { exact: true }).fill("3280");
    await page.getByLabel("CP", { exact: true }).fill("600");
    await expect(page.getByRole("button", { name: /^Quick Solve/ })).toBeEnabled();
  });
});

test.describe("a data set without consumables.json", () => {
  // Playwright cannot route a request the service worker answers.
  test.use({ serviceWorkers: "block" });

  test("has no meal or medicine picker", async ({ page }) => {
    let served = 0;
    await page.route("**/data/v/**/consumables.json", (route) => {
      served++;
      return route.fulfill({ status: 404, contentType: "application/json", body: '{"error":"not found"}' });
    });
    await openRecipeWithStats(page);

    await expect.poll(() => served).toBeGreaterThan(0);
    await expect(page.getByRole("heading", { name: PICKER })).toHaveCount(0);
    await expect(page.getByRole("combobox", { name: "Meal" })).toHaveCount(0);
    await expect(page.getByRole("button", { name: /^Quick Solve/ })).toBeEnabled();
  });
});
