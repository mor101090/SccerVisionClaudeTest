import { expect, test, type Page } from "@playwright/test";

import { GUEST_USER, TEST_USER } from "./fixtures";
import { ensureSignedIn } from "./helpers";

async function requestSub(page: Page, out: number, inn: number) {
  await page.getByLabel("Off #").fill(String(out));
  await page.getByLabel("On #").fill(String(inn));
  await page.getByRole("button", { name: "Request change" }).click();
  await expect(page.getByText(`#${out} off, #${inn} on`)).toBeVisible();
}

for (const mode of ["realtime", "polling"] as const) {
  test(`two players, one deterministic match (${mode}): identical result on both screens`, async ({
    browser,
  }) => {
    test.setTimeout(300_000);
    const hostCtx = await browser.newContext();
    const guestCtx = await browser.newContext();
    if (mode === "polling") {
      // Realtime unavailable (e.g. origin not allowed / not configured): both must fall back to polling.
      for (const ctx of [hostCtx, guestCtx])
        await ctx.route("**/api/lobbies/*/live", (r) =>
          r.fulfill({
            status: 503,
            contentType: "application/json",
            body: '{"error":"Realtime off"}',
          }),
        );
    }
    const host = await hostCtx.newPage();
    const guest = await guestCtx.newPage();

    await ensureSignedIn(host, TEST_USER);
    await ensureSignedIn(guest, GUEST_USER);

    // Host creates a game, guest joins with the code.
    await host.goto("/lobby");
    await host.getByRole("button", { name: "Host a game" }).click();
    await host.waitForURL(/\/lobby\/[0-9a-f-]{36}$/);
    const code = (await host.getByTestId("lobby-code").textContent())!.trim();
    expect(code).toMatch(/^\d{6}$/);

    await guest.goto("/lobby");
    await guest.getByLabel("Game code").fill(code);
    await guest.getByRole("button", { name: "Join" }).click();
    await guest.waitForURL(/\/lobby\/[0-9a-f-]{36}$/);

    await expect(host.getByTestId("lobby-status")).toHaveText("ready", { timeout: 15_000 });
    await host.getByRole("button", { name: "Kick off" }).click();
    await expect(guest.getByTestId("lobby-status")).toHaveText("playing", { timeout: 15_000 });

    // Realtime: updates are pushed. Polling: the fallback took over.
    await expect(host.getByTestId("sync-mode")).toHaveText(mode, { timeout: 20_000 });
    await expect(guest.getByTestId("sync-mode")).toHaveText(mode, { timeout: 20_000 });

    await host.getByRole("button", { name: "64×" }).click();
    await guest.getByRole("button", { name: "64×" }).click();

    // Both managers make a change during the match (host directly, guest via the server).
    await host.waitForTimeout(3_000);
    await requestSub(host, 9, 12);
    await requestSub(guest, 10, 14);

    await expect(host.getByTestId("checksum")).toBeVisible({ timeout: 240_000 });
    await expect(guest.getByTestId("checksum")).toBeVisible({ timeout: 60_000 });

    const hostSum = await host.getByTestId("checksum").textContent();
    const guestSum = await guest.getByTestId("checksum").textContent();
    const hostScore = await host.getByTestId("final-score").textContent();
    const guestScore = await guest.getByTestId("final-score").textContent();
    expect(guestSum).toBe(hostSum);
    expect(guestScore).toBe(hostScore);

    // Both substitutions really happened in the (shared) match.
    const subs = await host.evaluate(async (id) => {
      const r = await fetch(`/api/lobbies/${id}`);
      const s = (await r.json()) as { inputs: { frame: number | null; team: number }[] };
      return s.inputs.map((i) => ({ team: i.team, applied: i.frame !== null }));
    }, host.url().split("/").pop()!);
    expect(subs).toEqual(
      expect.arrayContaining([
        { team: 0, applied: true },
        { team: 1, applied: true },
      ]),
    );

    await hostCtx.close();
    await guestCtx.close();
  });
}
