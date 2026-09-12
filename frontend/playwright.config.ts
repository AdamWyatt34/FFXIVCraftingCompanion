import { defineConfig, devices } from "@playwright/test";

/**
 * End-to-end tests against a production build (`next start`), the only mode with the service
 * worker enabled. The build must point at the mock API (`NEXT_PUBLIC_API_URL=http://localhost:3101`),
 * because requests the service worker makes cannot be intercepted; `npm run test:e2e` builds it.
 */
export default defineConfig({
  testDir: "./e2e",
  timeout: 90_000,
  expect: { timeout: 15_000 },
  fullyParallel: false,
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? [["github"], ["html", { open: "never" }]] : "list",
  use: {
    baseURL: "http://localhost:3100",
    trace: "retain-on-failure",
    serviceWorkers: "allow",
  },
  projects: [{ name: "chromium", use: { ...devices["Desktop Chrome"] } }],
  webServer: [
    {
      command: "node e2e/mock-api.mjs",
      url: "http://localhost:3101/__requests",
      reuseExistingServer: !process.env.CI,
      timeout: 30_000,
    },
    {
      command: "npx next start -p 3100",
      url: "http://localhost:3100/offline",
      reuseExistingServer: !process.env.CI,
      timeout: 120_000,
    },
  ],
});
