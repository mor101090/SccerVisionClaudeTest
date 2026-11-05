/* eslint-disable no-console -- CLI script: progress goes to stdout. */
/**
 * Creates (or resets) the e2e accounts on the Supabase dev project: confirmed email, known password.
 * Idempotent; the daily e2e workflow runs it before Playwright.
 *
 *   NEXT_PUBLIC_SUPABASE_URL=… SUPABASE_SECRET_KEY=… node tools/e2e-seed.ts
 *
 * Never run against production: the passwords are in the repo (tests/e2e/fixtures.ts).
 */
import { createClient } from "@supabase/supabase-js";

import { GUEST_USER, TEST_USER } from "../tests/e2e/fixtures.ts";

const url = process.env.NEXT_PUBLIC_SUPABASE_URL;
const secret = process.env.SUPABASE_SECRET_KEY;
if (!url || !secret)
  throw new Error("NEXT_PUBLIC_SUPABASE_URL and SUPABASE_SECRET_KEY are required");
if (process.env.VERCEL_ENV === "production")
  throw new Error("Refusing to seed test accounts on production");

const admin = createClient(url, secret, { auth: { persistSession: false } }).auth.admin;

async function findByEmail(email: string) {
  for (let page = 1; ; page++) {
    const { data, error } = await admin.listUsers({ page, perPage: 1000 });
    if (error) throw error;
    const user = data.users.find((u) => u.email?.toLowerCase() === email);
    if (user || data.users.length < 1000) return user ?? null;
  }
}

for (const account of [TEST_USER, GUEST_USER]) {
  const existing = await findByEmail(account.email);
  const { error } = existing
    ? await admin.updateUserById(existing.id, { password: account.password, email_confirm: true })
    : await admin.createUser({
        email: account.email,
        password: account.password,
        email_confirm: true,
        user_metadata: { name: account.name },
      });
  if (error) throw error;
  console.log(`${existing ? "reset" : "created"} ${account.username}`);
}
