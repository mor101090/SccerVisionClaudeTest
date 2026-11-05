import { expect, test } from "@playwright/test";

import { TEST_USER } from "./fixtures";
import { ensureSignedIn } from "./helpers";

test("play a match, save it, find it in My matches, watch the replay", async ({ page }) => {
  test.setTimeout(180_000);

  await ensureSignedIn(page, TEST_USER);

  // Engine (WASM worker) loads, then the whole match is simulated.
  const random = page.getByRole("button", { name: "Random match" });
  await expect(random).toBeEnabled({ timeout: 60_000 });
  await random.click();
  await page.getByRole("button", { name: "64×" }).click();

  const save = page.getByRole("button", { name: "Save match" });
  await expect(save).toBeVisible({ timeout: 120_000 });
  await save.click();
  await expect(page.getByTestId("saved")).toBeVisible({ timeout: 30_000 });

  const score = (await page.getByTestId("score").textContent()) ?? "";

  await page.goto("/matches");
  await expect(page.getByTestId("match-list")).toBeVisible();

  await page.goto("/play");
  await page.goBack();
  await page.getByTestId("match-list").getByRole("link").first().click();
  await page.waitForURL(/\/matches\/[0-9a-f-]{36}$/);
  await expect(page.getByRole("heading", { level: 1 })).toContainText("–");
  // The replay downloads from the bucket and plays: the clock moves.
  await page.getByRole("button", { name: "64×" }).click();
  await expect(page.getByTestId("clock")).not.toHaveText("0'", { timeout: 20_000 });
  expect(score).toMatch(/\d+ – \d+/);
});
