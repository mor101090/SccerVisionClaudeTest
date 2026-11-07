import { expect, test, type Page } from "@playwright/test";

import { TEST_USER } from "./fixtures";
import { ensureSignedIn } from "./helpers";

const SHOTS = process.env.E2E_SHOTS_DIR;
async function shot(page: Page, name: string) {
  if (SHOTS) await page.screenshot({ path: `${SHOTS}/${name}.png`, fullPage: true });
}

/**
 * Create your team, end to end on the shared dev branch: manager → club → kits → squad (made by the team
 * generator) → player details → start playing → a match with the club's own squad.
 * Idempotent: an account that already has its club starts at the squad.
 */
test("create your team and play a match with it", async ({ page }) => {
  test.setTimeout(240_000);
  await page.setViewportSize({ width: 1440, height: 900 });
  await ensureSignedIn(page, TEST_USER);
  await page.goto("/team/new");
  await page.waitForURL(/\/team\/new\/(manager|club|kits|squad)/);

  if (page.url().endsWith("/manager")) {
    await expect(page.getByRole("heading", { name: "Tell us about you, coach" })).toBeVisible();
    await expect(page.getByLabel("Username")).toHaveValue(TEST_USER.username);
    await page.getByRole("button", { name: "Nationality" }).click();
    await page.getByRole("combobox", { name: "Search nationality" }).fill("isra");
    await page.getByRole("option", { name: "Israel" }).click();
    await expect(page.getByRole("button", { name: "Nationality" })).toHaveText(/Israel/);
    await page.getByLabel("User that invited you (optional)").fill("nobody_here_123");
    await page.getByRole("button", { name: "Continue" }).click();
    await expect(page.getByText("No manager with that username.")).toBeVisible();
    await shot(page, "1-manager");
    await page.getByLabel("User that invited you (optional)").fill("");
    await page.getByRole("button", { name: "Continue" }).click();
    await page.waitForURL("**/team/new/club");
  }

  if (page.url().endsWith("/club")) {
    await page.getByLabel("Team name").fill("Harbour Rovers");
    await expect(page.getByLabel("Short name")).toHaveValue("HAR");
    await page.getByLabel("Short name").fill("e2e");
    await expect(page.getByLabel("Short name")).toHaveValue("EE");
    await page.getByLabel("Short name").fill("rov");
    await shot(page, "2-club");
    await page.getByRole("button", { name: "Continue" }).click();
    await page.waitForURL("**/team/new/kits");
  }

  if (page.url().endsWith("/kits")) {
    // Blue is taken by the away kit: greyed out for the home shirt.
    await expect(page.getByRole("radio", { name: "Shirt: Blue" }).first()).toBeDisabled();
    await page.getByTitle("Green", { exact: true }).first().click();
    await expect(page.getByRole("radio", { name: "Shirt: Green" }).first()).toBeChecked();
    await shot(page, "3-kits");
    await page.getByRole("button", { name: "Generate my squad" }).click();
    await page.waitForURL("**/team/new/squad");
  }

  // Generating (first time): 22/22, the "Squad complete" celebration, then the squad. An account that already has
  // its club (every run after the first: the seed keeps the club) opens straight on the squad.
  const complete = page.getByRole("heading", { name: /squad complete/i });
  const squad = page.getByRole("heading", { name: "Suggested XI · 4-4-2" });
  await expect(complete.or(squad)).toBeVisible({ timeout: 60_000 });
  if (await complete.isVisible()) {
    await expect(page.getByText("22/22 players signed", { exact: false })).toBeVisible();
    await shot(page, "4-squad-complete");
  }
  await expect(squad).toBeVisible({ timeout: 60_000 });
  await expect(page.locator("tbody tr")).toHaveCount(22);
  await shot(page, "5-squad");

  // Player details: a bench player can become a starter; anyone can be captain.
  const bench = page
    .locator("tbody tr")
    .filter({ hasNot: page.getByTitle("In the starting XI") })
    .first();
  await bench.getByRole("button").click();
  const drawer = page.getByRole("dialog");
  await expect(drawer.getByText("DEFENSE · ").or(drawer.getByText("GOALKEEPING · "))).toBeVisible();
  await shot(page, "6-player");
  await drawer.getByRole("button", { name: "Make captain" }).click();
  await expect(drawer.getByRole("button", { name: "Captain" })).toBeDisabled();
  await drawer.getByRole("button", { name: "Set as starter" }).click();
  await expect(drawer.getByRole("button", { name: "Starter" })).toBeDisabled();
  await drawer.getByRole("button", { name: "Close" }).click();

  await page.getByRole("button", { name: "Start playing →" }).click();
  await page.waitForURL("**/home");
  await page.goto("/play");

  // The engine fields the club at home.
  const mine = page.getByRole("button", { name: /^Play with / });
  await expect(mine).toBeEnabled({ timeout: 60_000 });
  const club = (await mine.textContent())!.replace("Play with ", "");
  await mine.click();
  // The scoreboard names the club (exact text: not the "Play with …" button), and the match runs to full time.
  await expect(page.getByText(club, { exact: true }).first()).toBeVisible({ timeout: 60_000 });
  await page.getByRole("button", { name: "64×" }).click();
  await shot(page, "7-play");
  const save = page.getByRole("button", { name: "Save match" });
  await expect(save).toBeVisible({ timeout: 150_000 });
  // Saved with the club: the server accepts it only when the squad played is the stored one.
  await save.click();
  await expect(page.getByTestId("saved")).toBeVisible({ timeout: 30_000 });
});
