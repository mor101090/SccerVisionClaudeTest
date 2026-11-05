import { expect, test } from "@playwright/test";

import { SIGNUP_EMAIL, TEST_USER } from "./fixtures";
import { ensureSignedIn, fillSignIn } from "./helpers";

test.describe.serial("auth", () => {
  test("protected page redirects to login when signed out", async ({ page }) => {
    await page.goto("/matches");
    await expect(page).toHaveURL(/\/auth\/sign-in/);
    await expect(page.getByRole("heading", { name: "Login to your account" })).toBeVisible();
  });

  test("wrong password shows the banner and keeps the email", async ({ page }) => {
    await fillSignIn(page, TEST_USER.email, "definitely-wrong-password");
    await expect(
      // The banner, not the field note under Password (both say "Wrong email or password").
      page.getByRole("alert").filter({ hasText: "reset your password once" }),
    ).toBeVisible();
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
    // Confirm email on (dev, prod): Supabase answers with the same verify screen as for a new
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

    // The account exists (yarn e2e:seed), so Supabase shows the same verify screen as for a new one and sends nothing.
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

  test("no auth proxy on the site: password paths are gone, social sign-in goes to Supabase", async ({
    page,
    request,
  }) => {
    // The Neon-era proxy is gone: none of its paths answer (password flows run only as Server Actions).
    for (const path of ["sign-in/email", "sign-up/email", "reset-password", "get-session"]) {
      const res = await request.post(`/api/auth/${path}`, {
        data: { email: TEST_USER.email, password: "wrong-password" },
      });
      expect(res.status(), path).toBe(404);
    }
    // Google sign-in leaves for Supabase Auth's authorize endpoint (PKCE), back to /auth/callback.
    await page.goto("/auth/sign-in");
    const authorize = page.waitForRequest((r) =>
      r.url().includes("/auth/v1/authorize?provider=google"),
    );
    await page.getByRole("button", { name: "Google" }).click();
    const url = new URL((await authorize).url());
    expect(url.searchParams.get("redirect_to")).toContain("/auth/callback");
  });

  test("settings lists the social providers and Connect links one to this account", async ({
    page,
  }) => {
    await ensureSignedIn(page, TEST_USER);
    await page.getByRole("link", { name: "Settings" }).click();
    await expect(page.getByRole("heading", { name: "Connected accounts" })).toBeVisible();
    for (const provider of ["google", "facebook", "discord"]) {
      await expect(page.getByTestId(`provider-${provider}`)).toContainText("Not connected");
    }
    // Connect goes to Supabase's identity-linking endpoint (needs the session), back to /settings via the callback.
    const authorize = page.waitForRequest((r) =>
      r.url().includes("/auth/v1/user/identities/authorize"),
    );
    await page.getByRole("button", { name: "Connect Discord" }).click();
    const url = new URL((await authorize).url());
    expect(url.searchParams.get("provider")).toBe("discord");
    expect(url.searchParams.get("redirect_to")).toContain(
      "/auth/callback?redirectTo=%2Fsettings%3Flinking%3Ddiscord",
    );
  });

  test("a failed Connect comes back to settings with the reason", async ({ page }) => {
    await ensureSignedIn(page, TEST_USER);
    await page.goto(
      "/auth/callback?redirectTo=%2Fsettings%3Flinking%3Dgoogle&error=server_error&error_code=identity_already_exists",
    );
    await expect(page).toHaveURL(/\/settings\?linking=google&linkError=identity_already_exists/);
    await expect(
      page.getByRole("alert").filter({ hasText: "already used by another Soccer Vision account" }),
    ).toBeVisible();
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
