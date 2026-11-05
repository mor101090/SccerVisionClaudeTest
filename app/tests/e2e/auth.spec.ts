import { expect, test } from "@playwright/test";

import { SIGNUP_EMAIL, TEST_USER } from "./fixtures";
import { ensureSignedIn, fillSignIn } from "./helpers";

test.describe.serial("auth", () => {
  test("protected page redirects to login when signed out", async ({ page }) => {
    await page.goto("/matches");
    await expect(page).toHaveURL(/\/auth\/sign-in/);
    await expect(page.getByRole("heading", { name: "Login to your account" })).toBeVisible();
  });

  test("wrong password shows the banner with tries left and keeps the email", async ({ page }) => {
    await fillSignIn(page, TEST_USER.email, "definitely-wrong-password");
    await expect(page.getByRole("alert").filter({ hasText: /tries? left|paused/ })).toBeVisible();
    await expect(page.getByLabel("Email address")).toHaveValue(TEST_USER.email);
    await expect(page).toHaveURL(/\/auth\/sign-in/);
  });

  test("login (clears the failure counter), session survives reload, sign out", async ({
    page,
  }) => {
    await ensureSignedIn(page, TEST_USER);
    await page.reload();
    await expect(page.getByTestId("user-name")).toHaveText(TEST_USER.username);
    await page.getByRole("button", { name: "Sign out" }).click();
    // Signed out: back to the default site, the classic game.
    await page.waitForURL((url) => url.pathname === "/classic/index.html");
    await page.goto("/play");
    await expect(page.getByRole("link", { name: "Login" })).toBeVisible();
  });

  test("sign up validates every field on the server", async ({ page }) => {
    await page.goto("/auth/sign-up");
    await page.getByLabel("Email address").fill(TEST_USER.email);
    await page.getByLabel("Password", { exact: true }).fill("kickoff-2026");
    await page.getByLabel("Confirm password").fill("kickoff-2027");
    await page.getByRole("button", { name: "Sign up", exact: true }).click();
    await expect(page.getByText("The passwords don't match.")).toBeVisible();
    await expect(
      page.getByText("Please accept the Terms and Conditions to continue."),
    ).toBeVisible();

    await page.getByLabel("Confirm password").fill("kickoff-2026");
    await page.getByLabel(/I have read and agree/).check();
    await page.getByRole("button", { name: "Sign up", exact: true }).click();
    // Verification off: Neon says the address is taken. On (dev, prod): the same verify screen as a new
    // account, so sign-up never reveals which emails exist.
    await expect(
      page
        .getByText("That email is already registered.")
        .or(page.getByRole("heading", { name: "Check your inbox" })),
    ).toBeVisible();
  });

  test("sign up sends a code and opens the verify screen", async ({ page }) => {
    await page.goto("/auth/sign-up");
    await page.getByLabel("Email address").fill(SIGNUP_EMAIL);
    await page.getByLabel("Password", { exact: true }).fill("kickoff-2026");
    await page.getByLabel("Confirm password").fill("kickoff-2026");
    await page.getByLabel(/I have read and agree/).check();
    await page.getByRole("button", { name: "Sign up", exact: true }).click();

    // First run creates the account; later runs find it already registered.
    const verify = page.getByRole("heading", { name: "Check your inbox" });
    const taken = page.getByText("That email is already registered.");
    await expect(verify.or(taken)).toBeVisible({ timeout: 15_000 });
    if (await verify.isVisible()) {
      await expect(page.getByText(SIGNUP_EMAIL)).toBeVisible();
      await page.getByLabel("Digit 1").fill("000000");
      await page.getByRole("button", { name: "Verify & continue" }).click();
      await expect(page.getByRole("alert").filter({ hasText: /code/i })).toBeVisible();
    }
  });

  test("forgot password always answers 'check your email'", async ({ page }) => {
    await page.goto("/auth/forgot-password");
    await page.getByLabel("Email address").fill("nobody-here@soccer-vision.test");
    await page.getByRole("button", { name: "Send reset link" }).click();
    await expect(page.getByRole("heading", { name: "Check your email" })).toBeVisible();
    await expect(page.getByText("nobody-here@soccer-vision.test")).toBeVisible();
    await expect(page.getByRole("button", { name: /Resend link \(0:\d\d\)/ })).toBeDisabled();
  });

  test("a reset link without a valid token asks for a new one", async ({ page }) => {
    await page.goto("/auth/reset-password");
    await expect(page.getByText("This reset link expired or was already used.")).toBeVisible();
    await expect(page.getByRole("link", { name: "Send a new link" })).toBeVisible();
  });

  test("the auth proxy serves only the SDK paths: no password login that skips the lockout", async ({
    page,
    request,
  }) => {
    for (const path of [
      "sign-in/email",
      "sign-up/email",
      "request-password-reset",
      "reset-password",
      "email-otp/send-verification-otp",
      "sign-in/email-otp",
    ]) {
      const res = await request.post(`/api/auth/${path}`, {
        data: { email: TEST_USER.email, password: "wrong-password" },
      });
      expect(res.status(), path).toBe(404);
    }
    expect((await request.get("/api/auth/get-session")).status()).toBe(200);
    // Google sign-in still goes through the proxy and on to Google.
    await page.goto("/auth/sign-in");
    const social = page.waitForRequest((r) => r.url().includes("/api/auth/sign-in/social"));
    await page.getByRole("button", { name: "Google" }).click();
    const res = await (await social).response();
    expect(res?.status()).toBe(200);
  });

  test("a crafted redirect never leaves the site", async ({ page }) => {
    await ensureSignedIn(page, TEST_USER);
    for (const to of [
      "%2F%09%2Fevil.example",
      "%2F.%2F%2Fevil.example",
      "%2Fa%2F..%2F%2Fevil.example",
    ]) {
      await page.goto(`/auth/callback?redirectTo=${to}`);
      await page.waitForLoadState();
      expect(page.url(), to).not.toContain("evil.example");
      expect(new URL(page.url()).pathname, to).toBe("/play");
    }
  });
});
