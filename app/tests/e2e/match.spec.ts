import { expect, test } from "@playwright/test";

import { TEST_USER } from "./fixtures";
import { ensureSignedIn } from "./helpers";

test("instant result: full-time report, saved, found in My Games, replay plays", async ({
  page,
}) => {
  test.setTimeout(180_000);

  await ensureSignedIn(page, TEST_USER);
  await page.goto("/play");

  await page.getByRole("radio", { name: "Instant result" }).click();
  const start = page.getByRole("button", { name: "Start match" });
  // Engine (WASM worker) loads first.
  await expect(start).toBeEnabled({ timeout: 60_000 });
  await start.click();

  await expect(page.getByTestId("full-time")).toBeVisible({ timeout: 120_000 });
  await expect(page.getByTestId("saved")).toBeVisible({ timeout: 30_000 });
  const score = (await page.getByTestId("score").textContent()) ?? "";
  expect(score).toMatch(/\d+ – \d+/);

  await page.getByRole("link", { name: "Watch the replay" }).click();
  await page.waitForURL(/\/matches\/[0-9a-f-]{36}$/);
  await expect(page.getByTestId("score")).toHaveText(score.trim(), { timeout: 20_000 });
  // The replay downloads from the bucket and plays: the clock moves.
  await page.getByRole("button", { name: "16×" }).click();
  await expect(page.getByTestId("clock")).not.toHaveText(/^0'$/, { timeout: 20_000 });

  await page.goto("/matches");
  await expect(page.getByTestId("match-list")).toBeVisible();
});

test("watch live: pre-match, kick-off, a substitution and the tactics drawer", async ({ page }) => {
  test.setTimeout(180_000);

  await ensureSignedIn(page, TEST_USER);
  await page.goto("/play");
  const start = page.getByRole("button", { name: "Start match" });
  await expect(start).toBeEnabled({ timeout: 60_000 });
  await start.click();

  await expect(page.getByRole("heading", { name: "Pre-match" })).toBeVisible({ timeout: 30_000 });
  await page.getByRole("radio", { name: "Attacking" }).click();
  await page.getByRole("button", { name: "Kick off" }).click();

  await expect(page.getByTestId("scoreboard")).toBeVisible();
  await page.getByRole("button", { name: "16×" }).click();
  await expect(page.getByTestId("clock")).not.toHaveText(/^1'$/, { timeout: 30_000 });

  await page.getByRole("button", { name: /Substitutions/ }).click();
  const drawer = page.getByRole("dialog", { name: "Substitutions" });
  await drawer.getByRole("button").filter({ hasText: /\S/ }).nth(3).click(); // a player on the pitch
  const bench = drawer.locator("h3", { hasText: "Bench" }).locator("..").getByRole("button");
  await bench.first().click();
  await drawer.getByRole("button", { name: /Confirm 1 change/ }).click();
  await expect(page.getByRole("button", { name: /Substitutions/ })).toContainText("4 left");

  await page.getByRole("button", { name: "Tactics", exact: true }).click();
  await expect(page.getByRole("dialog", { name: "Tactics" })).toBeVisible();
  await page.keyboard.press("Escape");

  await page.getByRole("button", { name: "Leave match" }).click();
  await page.getByRole("dialog").getByRole("button", { name: "Leave match" }).click();
  await expect(page.getByRole("heading", { name: "Play a match" })).toBeVisible();
});
