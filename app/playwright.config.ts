import { defineConfig, devices } from "@playwright/test";

/**
 * E2E tests. Locally they run against `yarn dev` (or E2E_BASE_URL);
 * in CI they run against the Vercel preview URL of the PR.
 */
const baseURL = process.env.E2E_BASE_URL ?? "http://localhost:3100";

export default defineConfig({
  testDir: "./tests/e2e",
  timeout: 60_000,
  expect: { timeout: 15_000 },
  fullyParallel: false,
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? "github" : "list",
  use: {
    baseURL,
    trace: "retain-on-failure",
    // Vercel deployment protection bypass for previews (optional).
    extraHTTPHeaders: process.env.VERCEL_AUTOMATION_BYPASS_SECRET
      ? { "x-vercel-protection-bypass": process.env.VERCEL_AUTOMATION_BYPASS_SECRET }
      : undefined,
  },
  projects: [{ name: "chromium", use: { ...devices["Desktop Chrome"] } }],
  webServer: process.env.E2E_BASE_URL
    ? undefined
    : {
        command: "yarn dev --port 3100",
        url: baseURL,
        reuseExistingServer: true,
        timeout: 120_000,
      },
});
