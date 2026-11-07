import { expect, test } from "@playwright/test";

import { GUEST_USER, TEST_USER } from "./fixtures";
import { ensureSignedIn } from "./helpers";

test.describe("profile, clubs and leagues", () => {
  test("profile: record, edit the bio, achievements, menu links", async ({ page }) => {
    await ensureSignedIn(page, TEST_USER);
    await page.goto("/profile");
    await expect(page.getByRole("heading", { level: 1, name: "Your profile" })).toBeVisible();
    await expect(page.getByText("Matches", { exact: true })).toBeVisible();

    const bio = `Bio ${Date.now() % 100000}`;
    await page.getByRole("link", { name: "Edit profile" }).click();
    await page.getByLabel("About you (optional)").fill(bio);
    await page.getByRole("button", { name: "Save changes" }).click();
    await page.waitForURL("**/profile");
    await expect(page.getByText(bio)).toBeVisible();

    await page.goto("/profile/achievements");
    await expect(page.getByText(/of 12 earned/)).toBeVisible();

    await page.getByRole("button", { name: "Account menu" }).click();
    for (const name of ["My profile", "Club settings", "Invite a friend", "Support the game"]) {
      await expect(page.getByRole("link", { name })).toBeVisible();
    }
  });

  test("another manager and their club", async ({ page }) => {
    await ensureSignedIn(page, TEST_USER);
    await page.goto(`/managers/${GUEST_USER.username}`);
    await expect(page.getByRole("heading", { level: 1, name: GUEST_USER.username })).toBeVisible();
    await page.goto("/search?q=Harbour");
    await page.locator('a[href^="/clubs/"]').first().click();
    await expect(page.getByRole("link", { name: "Squad" }).first()).toBeVisible();
    await page
      .getByRole("navigation", { name: "Club" })
      .getByRole("link", { name: "Squad" })
      .click();
    await expect(page.locator("tbody tr")).toHaveCount(22);
  });

  test("club settings save, delete needs the exact name", async ({ page }) => {
    await ensureSignedIn(page, TEST_USER);
    await page.goto("/club/settings");
    const city = `City ${Date.now() % 1000}`;
    await page.getByLabel("Home city (optional)").fill(city);
    await page.getByRole("button", { name: "Save changes" }).click();
    await expect(page.getByText("Club settings saved.")).toBeVisible();
    await page.reload();
    await expect(page.getByLabel("Home city (optional)")).toHaveValue(city);

    await page.getByRole("button", { name: "Delete club…" }).click();
    const dialog = page.getByRole("dialog");
    await expect(dialog.getByRole("button", { name: "Delete club" })).toBeDisabled();
    await dialog.getByLabel("Type the club name to confirm").fill("not it");
    await expect(dialog.getByRole("button", { name: "Delete club" })).toBeDisabled();
    await dialog.getByRole("button", { name: "Cancel" }).click();
    await expect(dialog).toBeHidden();
  });

  test("invite link: the friend's page remembers who invited", async ({ page, browser }) => {
    await ensureSignedIn(page, TEST_USER);
    await page.goto("/invite");
    const link = await page.getByRole("textbox", { name: "Your invite link" }).inputValue();
    expect(link).toMatch(new RegExp(`/i/${TEST_USER.username}$`));

    const visitor = await browser.newContext();
    const friend = await visitor.newPage();
    await friend.goto(new URL(link).pathname);
    await expect(friend.getByRole("heading", { level: 1 })).toContainText("Build your club");
    await friend.getByRole("link", { name: "Create my club" }).click();
    await friend.waitForURL(/\/auth\/sign-up/);
    const cookies = await visitor.cookies();
    expect(cookies.find((c) => c.name === "sv_invite")?.value).toBe(TEST_USER.username);
    await visitor.close();
  });

  test("support page and leagues render; unknown pages get the 404", async ({ page }) => {
    await ensureSignedIn(page, TEST_USER);
    await page.goto("/support");
    await expect(
      page.getByRole("heading", { level: 1, name: "Support SoccerVision" }),
    ).toBeVisible();
    await expect(page.getByRole("radio", { name: "€5" })).toBeChecked();
    await page.goto("/leagues");
    await expect(page.getByRole("heading", { level: 1, name: "Leagues" })).toBeVisible();
    const res = await page.goto("/no-such-page");
    expect(res?.status()).toBe(404);
    await expect(page.getByRole("heading", { name: "This page went out of play" })).toBeVisible();
  });
});
