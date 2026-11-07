import { expect, test } from "@playwright/test";

/**
 * Regressions from the QA audit (docs/qa/2026-11-06-qa-ux-audit.md). No account needed: public pages and the
 * classic game.
 */

test.describe("classic page", () => {
  test("renders in standards mode and fits a phone (BUG-01)", async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto("/classic/index.html");
    const doc = await page.evaluate(() => ({
      mode: document.compatMode,
      lang: document.documentElement.lang,
      viewport: !!document.querySelector('meta[name="viewport"]'),
      overflow: document.documentElement.scrollWidth > innerWidth + 1,
    }));
    expect(doc).toEqual({ mode: "CSS1Compat", lang: "en", viewport: true, overflow: false });
  });

  test("kits open at the top with only this mode's button; Start match waits for the engine (BUG-05, BUG-09, BUG-16)", async ({
    page,
  }) => {
    await page.goto("/classic/index.html");
    await page.locator("#asUser").click();
    expect(await page.locator("#kitSetup").evaluate((el) => el.scrollTop)).toBe(0);
    await expect(page.locator("#kitButtons button:visible")).toHaveCount(1);

    await page.locator("#kitGo").click();
    const kickoff = page.locator("#kickoff");
    // Pressed at once it used to be dropped without a word; now it is disabled until the engine has the match.
    await expect(kickoff).toBeDisabled();
    await expect(kickoff).toHaveText(/Preparing/);
    await expect(kickoff).toBeEnabled({ timeout: 60_000 });
    await expect(kickoff).toHaveText("Start match");
  });

  test("an invite link hides the computer-only settings (BUG-09)", async ({ page }) => {
    await page.goto("/classic/index.html#game=123456");
    await expect(page.locator("#kitJoin")).toBeVisible();
    await expect(page.locator("#kitStrength")).toBeHidden();
    expect(await page.locator("#kitSetup").evaluate((el) => el.scrollTop)).toBe(0);
  });

  test("settings show no empty sections and close on Escape (BUG-07)", async ({ page }) => {
    await page.goto("/classic/index.html");
    await page.locator("#asUser").click();
    await page.locator("#kitGo").click();
    await expect(page.locator("#kickoff")).toBeEnabled({ timeout: 60_000 });
    await page.locator("#gear").click();
    await expect(page.locator("#settings h3:visible")).toHaveText(["Display"]);
    await page.keyboard.press("Escape");
    await expect(page.locator("#settings")).toBeHidden();
  });
});

test.describe("public pages", () => {
  for (const path of ["/home", "/rules", "/terms", "/play", "/auth/sign-in"]) {
    test(`${path} has one main landmark and one h1 (BUG-06)`, async ({ page }) => {
      await page.goto(path);
      await expect(page.locator("main")).toHaveCount(1);
      await expect(page.locator("h1")).toHaveCount(1);
    });
  }

  test("favicon, robots and sitemap answer (BUG-12)", async ({ request }) => {
    for (const path of ["/favicon.ico", "/robots.txt", "/sitemap.xml"]) {
      expect((await request.get(path)).status(), path).toBe(200);
    }
  });

  test("an empty login focuses the first invalid field (BUG-11)", async ({ page }) => {
    await page.goto("/auth/sign-in");
    await page.getByRole("button", { name: "Login", exact: true }).click();
    await expect(page.getByLabel("Email address")).toBeFocused();
  });
});
