import { expect, test } from "@playwright/test";

test("the classic game loads and starts its engine", async ({ page }) => {
  const errors: string[] = [];
  page.on("pageerror", (e) => errors.push(e.message));
  await page.goto("/classic");
  await expect(page).toHaveURL(/\/classic\/index\.html$/);
  await expect(page.getByText("How do you want to play?", { exact: false })).toBeVisible();
  await page.getByText("Developer", { exact: true }).click();
  // The engine header (team line-ups) arrives from the WASM worker.
  await expect(page.getByText("Starting XI", { exact: false }).first()).toBeVisible({
    timeout: 60_000,
  });
  expect(errors).toEqual([]);
});
