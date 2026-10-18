import { expect, test } from "@playwright/test";
import AxeBuilder from "@axe-core/playwright";

/**
 * axe-core over the pages a visitor lands on. Serious and critical WCAG 2.1 A/AA violations fail
 * the run; the full violation list is in the assertion message.
 */
const PAGES = [
  "/",
  "/recipes",
  "/recipes/37783-pure-cosmic-tool-compatible-gel",
  "/recipes/by-job/cul/91-100",
  "/recipes/dawntrail",
  "/simulator",
  "/simulator?recipeId=37783",
  "/gear",
  "/recipes/plan",
  "/auth/login",
];

test("key pages have no serious or critical accessibility violations", async ({ page }) => {
  for (const path of PAGES) {
    await page.goto(path);
    await page.waitForLoadState("networkidle");
    const results = await new AxeBuilder({ page })
      .withTags(["wcag2a", "wcag2aa", "wcag21a", "wcag21aa"])
      .analyze();
    const blocking = results.violations
      .filter((violation) => violation.impact === "serious" || violation.impact === "critical")
      .map(
        (violation) =>
          `${path}: ${violation.id} (${violation.impact}) ${violation.help}\n` +
          violation.nodes.map((node) => `    ${node.html.slice(0, 160)}`).join("\n"),
      );
    expect(blocking, path).toEqual([]);
  }
});
