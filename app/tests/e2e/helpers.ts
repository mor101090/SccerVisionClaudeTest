import { expect, type Page } from "@playwright/test";

type User = { name: string; username: string; email: string; password: string };

export async function fillSignIn(page: Page, email: string, password: string): Promise<void> {
  await page.goto("/auth/sign-in");
  await page.getByLabel("Email address").fill(email);
  await page.getByLabel("Password", { exact: true }).fill(password);
  await page.getByRole("button", { name: "Login", exact: true }).click();
}

/** Completes the "Almost there" step (username + terms) if this account has not done it yet. */
async function finishProfileIfAsked(page: Page, user: User): Promise<void> {
  await page.waitForURL(/\/(play|auth\/finish)/, { timeout: 15_000 });
  if (!page.url().includes("/auth/finish")) return;
  await page.getByLabel("Username").fill(user.username);
  const terms = page.getByLabel(/I have read and agree/);
  if (await terms.isVisible()) await terms.check();
  await page.getByRole("button", { name: "Create account & build my team" }).click();
  await page.waitForURL("**/play");
}

/**
 * Signs in an existing test account on the shared dev branch (accounts are verified there).
 * Lands on /play with the profile complete.
 */
export async function ensureSignedIn(page: Page, user: User): Promise<void> {
  await fillSignIn(page, user.email, user.password);
  await finishProfileIfAsked(page, user);
  await expect(page.getByTestId("user-name")).toHaveText(user.username);
}
