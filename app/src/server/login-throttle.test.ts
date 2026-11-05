import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

const sql = readFileSync(
  join(dirname(fileURLToPath(import.meta.url)), "../../drizzle/0003_password_lockout_hook.sql"),
  "utf8",
);

describe("password lockout hook", () => {
  it("counts a direct failed password grant and blocks only a new sign-in during a pause", () => {
    expect(sql).toContain("INSERT INTO public.login_attempts");
    expect(sql).toContain("hook_counted_at");
    expect(sql).toContain("'decision', 'reject'");
    expect(sql).toContain("'message', 'Invalid login credentials'");
    expect(sql).toContain("'should_logout_user', false");
    expect(sql).not.toContain("paused");
  });
});
