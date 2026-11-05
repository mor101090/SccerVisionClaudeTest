import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

const sql = readFileSync(
  join(dirname(fileURLToPath(import.meta.url)), "../../drizzle/0003_login_pause.sql"),
  "utf8",
);

describe("login pause migration", () => {
  it("records when a pause began and does not install the paid password hook", () => {
    expect(sql).toContain("locked_at");
    expect(sql).toContain("DROP FUNCTION IF EXISTS public.hook_password_verification_attempt");
    expect(sql).not.toContain(
      "CREATE OR REPLACE FUNCTION public.hook_password_verification_attempt",
    );
  });
});
